CREATE TYPE ingredient AS
    (
    name                  TEXT,
    grams                 INT,
    calories_pr_hectogram INT,
    fats_pr_hectogram     INT,
    carbs_pr_hectogram    INT,
    protein_pr_hectogram  INT,
    multiplier            FLOAT
    );

CREATE TYPE recipe_macros AS
    (
    total_calories FLOAT,
    total_fats     FLOAT,
    total_carbs    FLOAT,
    total_protein  FLOAT
    );

CREATE TABLE recipes
(
    id          SERIAL PRIMARY KEY,
    meal_type   CHAR(1),
    name        TEXT,
    image       TEXT,
    ingredients ingredient[],
    macros      recipe_macros
);

DROP TABLE recipes;

INSERT INTO recipes (meal_type, name, image, ingredients, macros)
VALUES ('D',
        'Test Meal',
        'TestImage',
        ARRAY [
            ROW ('banana',60, 11, 33, 55, 12, 0.6)::ingredient,
        ROW ('apple', 80, 11, 10, 9, 8, 0.9)::ingredient
            ],
        (647.3, 33.1, 88.9, 22.3)::recipe_macros);


INSERT INTO recipes (meal_type, name, image, ingredients, macros)
VALUES ('L',
        'Testy type',
        'TestImage2',
        ARRAY [
            ROW ('Pineapple',44, 123, 74, 95, 31, 0.4)::ingredient,
        ROW ('Grenade', 160, 6, 33, 5, 1, 0.3)::ingredient
            ],
        (3072, 97, 33, 77)::recipe_macros);

UPDATE recipes
SET image = 'TestImage.png'
WHERE id = 1;
UPDATE recipes
SET image = 'TestImage2.jpeg'
WHERE id = 2;

SELECT *
FROM recipes;

SELECT (macros).total_calories, (macros).total_carbs, (macros).total_fats, (macros).total_protein
FROM recipes;

SELECT ingredients
FROM recipes;

SELECT (macros).total_calories, (macros).total_carbs, (macros).total_fats, (macros).total_protein
FROM recipes;

SELECT ingredients
FROM recipes
WHERE id = 2;

SELECT unnest(ingredients) AS ingredient
FROM recipes
WHERE id = 2;

WITH ingredients_cte AS (SELECT id, unnest(ingredients) AS ingredient
                         FROM recipes
                         WHERE id = 2)
SELECT r.id, r.meal_type, r.name, r.image, i.ingredient, r.macros
FROM recipes r
         JOIN ingredients_cte i ON r.id = i.id
WHERE r.id = 2;

SELECT id,
       meal_type,
       name,
       image,
       macros,
       ingredients[1] AS ingredient_1,
       ingredients[2] AS ingredient_2,
       ingredients[3] AS ingredient_3,
       ingredients[4] AS ingredient_4,
       ingredients[5] AS ingredient_5
FROM recipes
WHERE id = 2;

CREATE OR REPLACE FUNCTION get_recipe_with_dynamic_ingredients(recipe_id INT)
    RETURNS TABLE
            (
                id         INT,
                meal_type  CHAR(1),
                name       TEXT,
                image      TEXT,
                ingredient TEXT[]
            )
AS
$$
DECLARE
recipe        RECORD;
    ingredients   TEXT[];
    dynamic_query TEXT;
    i             INT;
BEGIN
SELECT id, meal_type, name, image, ingredients
INTO recipe
FROM recipes
WHERE id = recipe_id;

ingredients := recipe.ingredients;

    dynamic_query := 'SELECT ' || recipe.id || ', ''' || recipe.meal_type || ''', ''' || recipe.name || ''', ''' ||
                     recipe.image || ''',';

FOR i IN 1 .. array_length(ingredients, 1)
        LOOP
            dynamic_query := dynamic_query || ' ' || quote_literal(ingredients[i]) || ' AS ingredient_' || i || ',';
END LOOP;

    -- Remove trailing comma and append FROM clause
    dynamic_query := left(dynamic_query, length(dynamic_query) - 1) || ' FROM recipes WHERE id = ' || recipe_id;

RETURN QUERY EXECUTE dynamic_query;
END;
$$ LANGUAGE plpgsql;

DROP FUNCTION get_recipe_with_dynamic_ingredients;

SELECT *
FROM get_recipe_with_dynamic_ingredients(1);

SELECT ingredient.name,
       ingredient.grams,
       ingredient.calories_pr_hectogram,
       ingredient.carbs_pr_hectogram,
       ingredient.protein_pr_hectogram,
       ingredient.fats_pr_hectogram,
       ingredient.multiplier
FROM recipes,
     unnest(ingredients) as ingredient
WHERE recipes.id = 2;

INSERT INTO recipes (meal_type, name, image, ingredients, macros)
VALUES ('L',
        'Testy type',
        'TestImage2',
        ARRAY [
            ROW ('Pineapple',44, 123, 74, 95, 31, 0.4)::ingredient,
        ROW ('Grenade', 160, 6, 33, 5, 1, 0.3)::ingredient
            ],
        (3072, 97, 33, 77)::recipe_macros);


INSERT INTO recipes (meal_type, name, image, macros)
VALUES ('L',
        'Other',
        'TestImage2',
        (3072, 97, 33, 77)::recipe_macros);

UPDATE recipes
SET ingredients = array_append(ingredients, ROW ('Pineapple',44, 123, 74, 95, 31, 0.4)::ingredient)
WHERE id = 3;

UPDATE recipes
SET macros = (4, 3, 2, 1)::recipe_macros
WHERE id IN (SELECT COUNT(*) FROM recipes);

UPDATE recipes
SET macros = (4, 3, 2, 1)::recipe_macros
WHERE id = 8;

SELECT COUNT(*)
FROM recipes;

DELETE
FROM recipes;

SELECT *
FROM recipes;

ALTER SEQUENCE recipes_id_seq RESTART WITH 1;

SELECT sequence_name
FROM information_schema.recipes
WHERE table_name = 'recipes'
  AND column_name = 'id';

SELECT id,
       name,
       meal_type,
       image,
       (macros).total_protein,
       (macros).total_fats,
       (macros).total_carbs,
       (macros).total_calories
FROM recipes;

DELETE
FROM recipes
WHERE id = 2;

SELECT setval('recipes_id_seq', (SELECT MAX(id) FROM recipes));

TRUNCATE TABLE recipes RESTART IDENTITY;

-- These three queries resets the id order correctly. Remember to delete the temporary table.
CREATE TEMP TABLE temp_recipes AS
SELECT *, ROW_NUMBER() OVER (ORDER BY id) as new_id
FROM recipes;
UPDATE recipes
SET id = temp_recipes.new_id
    FROM temp_recipes
WHERE recipes.id = temp_recipes.id;
SELECT setval('recipes_id_seq', (SELECT MAX(id) FROM recipes));


DROP TABLE temp_recipes;