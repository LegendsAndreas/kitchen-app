/* Create */
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

CREATE TABLE db_variables (id SERIAL PRIMARY KEY, name TEXT, value TEXT);

INSERT INTO db_variables (name, value)
VALUES ('recipes_count', (SELECT COUNT(*)::text FROM recipes));
INSERT INTO db_variables (name, value)
VALUES ('ingredients_count', (SELECT COUNT(*)::text FROM ingredients));

UPDATE db_variables
SET value = (SELECT COUNT(*)::text FROM recipes)
WHERE id = 1;

CREATE TABLE recipes
(
    id          SERIAL PRIMARY KEY,
    meal_type   CHAR(1),
    name        TEXT,
    image       TEXT,
    ingredients ingredient[],
    macros      recipe_macros
);

CREATE TABLE ingredients (
    id SERIAL PRIMARY KEY,
    name TEXT,
    cals FLOAT,
    fats FLOAT,
    carbs FLOAT,
    protein FLOAT,
    image TEXT,
    cost_per_100g FLOAT
);

CREATE TABLE sought_after_items
(
    item_id    INTEGER,
    name  VARCHAR(256),
    image TEXT,
    type VARCHAR(256),
    price DECIMAL
);

SELECT * FROM ingredients WHERE id = 1;

SELECT * FROM recipes WHERE id = 1;

INSERT INTO sought_after_items

DROP TABLE sought_after_items;

/*CREATE TABLE sought_after_items
(
    id SERIAL PRIMARY KEY,
    item_id    INTEGER,
    name  VARCHAR(256),
    image TEXT,
    type VARCHAR(256),
    price INTEGER,
    CONSTRAINT FK_ingredient_id FOREIGN KEY (item_id) REFERENCES ingredients (id) ON DELETE CASCADE
);*/

/* Insert */
INSERT INTO sought_after_items (id, name, image, price) VALUES (1, 'test', 'image', 25);
INSERT INTO sought_after_items (id, name, image, price) VALUES (111, 'test', 'image', 25);
INSERT INTO sought_after_items (id, name, image, price) VALUES (112, 'test', 'image', 25);

INSERT INTO ingredients (name) VALUES ('test_ing');

INSERT INTO recipe_instructions (instructions)
VALUES ('{
  "name": "Chocolate Chip Cookies",
  "steps": [
    {
      "step_number": 1,
      "instruction": "Preheat oven to 350°F (175°C)."
    },
    {
      "step_number": 2,
      "instruction": "In a bowl, mix flour, baking soda, and salt."
    },
    {
      "step_number": 3,
      "instruction": "In another bowl, cream together butter and sugar."
    },
    {
      "step_number": 4,
      "instruction": "Add eggs and vanilla extract to the butter mixture."
    },
    {
      "step_number": 5,
      "instruction": "Gradually add the dry ingredients and chocolate chips."
    },
    {
      "step_number": 6,
      "instruction": "Scoop dough onto a baking sheet and bake for 10-12 minutes."
    }
  ],
  "notes": [
    {
      "note_number": 1,
      "note": "Remember to..."
    },
    {
      "note_number": 2,
      "note": "By the way..."
    }
  ]
}');

INSERT INTO recipes (meal_type, name, image, ingredients, macros)
VALUES ('L',
        'Testy type',
        'TestImage2',
        ARRAY [
            ROW ('Pineapple', 44, 123, 74, 95, 31, 0.4)::ingredient,
            ROW ('Grenade', 160, 6, 33, 5, 1, 0.3)::ingredient
            ],
        (3072, 97, 33, 77): : recipe_macros);


INSERT INTO recipes (meal_type, name, image, macros)
VALUES ('L',
        'Other',
        'TestImage2',
        (3072, 97, 33, 77): : recipe_macros);

INSERT INTO recipes (meal_type, name, image, ingredients, macros)
VALUES ('D',
        'Test Meal',
        'TestImage',
        ARRAY [
            ROW ('banana', 60, 11, 33, 55, 12, 0.6)::ingredient,
            ROW ('apple', 80, 11, 10, 9, 8, 0.9)::ingredient
            ],
        (647.3, 33.1, 88.9, 22.3): : recipe_macros);


INSERT INTO recipes (meal_type, name, image, ingredients, macros)
VALUES ('L',
        'Testy type',
        'TestImage2',
        ARRAY [
            ROW ('Pineapple', 44, 123, 74, 95, 31, 0.4)::ingredient,
            ROW ('Grenade', 160, 6, 33, 5, 1, 0.3)::ingredient
            ],
        (3072, 97, 33, 77):: recipe_macros);

UPDATE recipes
SET image = 'TestImage.png'
WHERE id = 1;
UPDATE recipes
SET image = 'TestImage2.jpeg'
WHERE id = 2;

/* SELECT statements */
SELECT r.id, i.name
FROM recipes AS r, unnest(r.ingredients) AS i
GROUP BY r.id, i.name
LIMIT 1;

SELECT r.id,
       r.name,
       r.meal_type,
       r.image,
       r.cost,
       json_agg(
               json_build_object(
                       'name', i.name,
                       'grams', i.grams,
                       'calories_pr_hectogram', i.calories_pr_hectogram,
                       'fats_pr_hectogram', i.fats_pr_hectogram,
                       'carbs_pr_hectogram', i.carbs_pr_hectogram,
                       'protein_pr_hectogram', i.protein_pr_hectogram,
                       'multiplier', i.multiplier,
                    'cost', i.cost_per_100g
               )
       ) AS ingredients
FROM recipes r,
     unnest(r.ingredients) AS i
GROUP BY r.id
ORDER BY r.id
LIMIT 1;

SELECT r.name,
       (r.macros).total_calories,
       (r.macros).total_carbs,
       (r.macros).total_fats,
       (r.macros).total_protein,
       json_agg(
               json_build_object(
                       'name', i.name,
                       'grams', i.grams,
                       'calories_pr_hectogram', i.calories_pr_hectogram,
                       'fats_pr_hectogram', i.fats_pr_hectogram,
                       'carbs_pr_hectogram', i.carbs_pr_hectogram,
                       'protein_pr_hectogram', i.protein_pr_hectogram,
                       'multiplier', i.multiplier
               )
       )
FROM recipes r,
     unnest(r.ingredients) AS i
GROUP BY r.id
ORDER BY r.id;

SELECT name, json_agg(ingredients) AS ingredients_json
FROM recipes
GROUP BY id
ORDER BY id;


SELECT instruction.instructions -> 'name'
FROM recipe_instructions AS instruction
         INNER JOIN recipes ON instruction.instructions ->> 'name' = recipes.name;

SELECT * FROM ingredients WHERE id = 111;

SELECT *
FROM ingredients
ORDER BY id;

SELECT id, name, cals, fats, carbs, protein, image
FROM ingredients
ORDER BY id;

SELECT COUNT(*)
FROM recipes;

SELECT *
FROM recipes
ORDER BY id;

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

SELECT id,
       name,
       meal_type,
       image,
       (macros).total_protein,
       (macros).total_fats,
       (macros).total_carbs,
       (macros).total_calories
FROM recipes
ORDER BY id;

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

/* Update */
UPDATE ingredients
SET name    = 'test',
    cals    = 444,
    fats    = 444,
    carbs   = 444,
    protein = 444,
    image   = 'fuck'
WHERE id = 40;

CREATE
    OR REPLACE FUNCTION get_recipe_with_dynamic_ingredients(recipe_id INT)
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
    recipe RECORD;
    ingredients
           TEXT[];
    dynamic_query
           TEXT;
    i
           INT;
BEGIN
    SELECT id, meal_type, name, image, ingredients
    INTO recipe
    FROM recipes
    WHERE id = recipe_id;

    ingredients
        := recipe.ingredients;

    dynamic_query
        := 'SELECT ' || recipe.id || ', ''' || recipe.meal_type || ''', ''' || recipe.name || ''', ''' ||
           recipe.image || ''',';

    FOR i IN 1 .. array_length(ingredients, 1)
        LOOP
            dynamic_query := dynamic_query || ' ' || quote_literal(ingredients[i]) || ' AS ingredient_' || i || ',';
        END LOOP;

    -- Remove trailing comma and append FROM clause
    dynamic_query
        := left(dynamic_query, length(dynamic_query) - 1) || ' FROM recipes WHERE id = ' || recipe_id;

    RETURN QUERY EXECUTE dynamic_query;
END;
$$
    LANGUAGE plpgsql;

/* Delete/Drop */
DELETE FROM ingredients WHERE id = 111;

DELETE
FROM recipes;

DELETE
FROM recipes
WHERE id = 2;

DELETE
FROM recipe_instructions
WHERE recipe_id = 1;
DELETE

FROM recipes
WHERE id = 1;

DROP TABLE sought_after_items;
DROP TABLE recipes;

DROP FUNCTION get_recipe_with_dynamic_ingredients;

SELECT *
FROM get_recipe_with_dynamic_ingredients(1);

UPDATE recipes
SET ingredients = array_append(ingredients, ROW ('Pineapple', 44, 123, 74, 95, 31, 0.4)::ingredient)
WHERE id = 3;

UPDATE recipes
SET macros = (4, 3, 2, 1)::recipe_macros
WHERE id IN (SELECT COUNT(*) FROM recipes);

UPDATE recipes
SET macros = (4, 3, 2, 1)::recipe_macros
WHERE id = 8;



ALTER SEQUENCE recipes_id_seq RESTART WITH 1;

SELECT sequence_name
FROM information_schema.recipes
WHERE table_name = 'recipes'
  AND column_name = 'id';


SELECT setval('recipes_id_seq', (SELECT MAX(id) FROM recipes));

TRUNCATE TABLE recipes RESTART IDENTITY;

-- These three queries resets the id order correctly. Remember to delete the temporary table.
CREATE
    TEMP TABLE temp_recipes AS
SELECT *, ROW_NUMBER() OVER (ORDER BY id) as new_id
FROM recipes;
UPDATE recipes
SET id = temp_recipes.new_id
FROM temp_recipes
WHERE recipes.id = temp_recipes.id;
SELECT setval('recipes_id_seq', (SELECT MAX(id) FROM recipes));
DROP TABLE temp_recipes;

CREATE
    TEMP TABLE temp_instructions AS
SELECT *, ROW_NUMBER() OVER (ORDER BY id) as new_id
FROM recipe_instructions;
UPDATE recipe_instructions
SET id = temp_instructions.new_id
FROM temp_instructions
WHERE recipe_instructions.id = temp_instructions.id;
SELECT setval('recipes_id_seq', (SELECT MAX(id) FROM recipe_instructions));

DROP TABLE temp_recipes;

CREATE TABLE recipe_instructions
(
    id           SERIAL PRIMARY KEY,
    recipe_id    INT,
    instructions JSON,
    CONSTRAINT FK_recipe_id FOREIGN KEY (recipe_id) REFERENCES recipes (id)
);

ALTER TABLE recipe_instructions
    DROP CONSTRAINT fk_recipe_id,
    ADD CONSTRAINT fk_recipe_id
        FOREIGN KEY (id)
            REFERENCES recipes (id)
            ON DELETE
                CASCADE;

ALTER TABLE recipe_instructions
    ADD recipe_id INT;
ALTER TABLE recipe_instructions
    ADD CONSTRAINT FK_recipe_id FOREIGN KEY (recipe_id) REFERENCES recipes (id);

SELECT id, instructions, recipe_id
FROM recipe_instructions;
UPDATE recipe_instructions
SET recipe_id = 5
WHERE id = 1;

SELECT count(*)                                 AS total_connections,
       count(*) FILTER (WHERE state = 'active') AS active_connections,
       count(*) FILTER (WHERE state = 'idle')   AS idle_connections
FROM pg_stat_activity;

SELECT *
FROM recipes
WHERE id > 5
  AND id < 10
ORDER BY id;
SELECT *
FROM recipes
WHERE (macros).total_protein > 20
  AND (macros).total_protein < 40
ORDER BY (macros).total_protein;
SELECT *
FROM recipes
WHERE (macros).total_calories > 300
  AND (macros).total_calories < 700
ORDER BY (macros).total_calories;


SELECT *
FROM recipe_instructions;

SELECT instructions ->> 'name' AS instruction_name
FROM recipe_instructions;

SELECT id
FROM recipes
WHERE id IN (SELECT recipe_instructions.recipe_id FROM recipe_instructions);
SELECT instructions ->> 'name' AS recipe_name
FROM recipe_instructions
WHERE recipe_id IN (SELECT id FROM recipes);
SELECT id
FROM recipes
WHERE name IN (SELECT instructions ->> 'name' AS recipe_name
               FROM recipe_instructions);

-- The good one.
UPDATE recipe_instructions
SET recipe_id =
            (SELECT id FROM recipes WHERE name = instructions ->> 'name');

UPDATE recipe_instructions
SET recipe_id = 10
WHERE id = 1;
UPDATE recipe_instructions
SET recipe_id = 11
WHERE id = 2;

UPDATE recipe_instructions
SET instructions = jsonb_set(instructions::jsonb, '{name}', '"Kartoffelmos"'::jsonb)
WHERE id = 2;

SELECT *
FROM recipe_instructions;

SELECT *
FROM recipes
ORDER BY id DESC;

SELECT id,
       name,
       image,
       meal_type,
       (macros).total_calories,
       (macros).total_fats,
       (macros).total_carbs,
       (macros).total_protein
FROM recipes
WHERE (macros).total_protein >= 20
  AND (macros).total_protein <= 40
ORDER BY (macros).total_protein DESC;

SELECT nextval('recipes_id_seq'); -- Ensures the session initializes the sequence
SELECT currval('recipes_id_seq');
SELECT setval('recipes_id_seq', MAX(id))
FROM recipes;

SELECT COUNT(*) FROM recipes;

SELECT * FROM recipes WHERE id = 47;