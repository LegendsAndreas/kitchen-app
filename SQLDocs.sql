/* CREATE */
CREATE TYPE ingredient AS
(
    name                  TEXT,
    grams                 FLOAT,
    calories_pr_hectogram FLOAT,
    fat_pr_hectogram      FLOAT,
    carbs_pr_hectogram    FLOAT,
    protein_pr_hectogram  FLOAT
);

CREATE TYPE recipe_macros AS
(
    total_calories FLOAT,
    total_fat      FLOAT,
    total_carbs    FLOAT,
    total_protein  FLOAT
);

CREATE TABLE ingredients
(
    id                    SERIAL PRIMARY KEY,
    name                  TEXT,
    calories_pr_hectogram FLOAT,
    fat_pr_hectogram      FLOAT,
    carbs_pr_hectogram    FLOAT,
    protein_pr_hectogram  FLOAT,
    base64_image          TEXT
);

CREATE TABLE recipes
(
    id          SERIAL PRIMARY KEY,
    meal_type   CHAR(1),
    name        TEXT,
    image       TEXT,
    ingredients ingredient[],
    macros      recipe_macros,
    user_id     INT,
    CONSTRAINT FK_user_id FOREIGN KEY (user_id) REFERENCES users (id)
);

CREATE TABLE users
(
    id       SERIAL PRIMARY KEY,
    username TEXT,
    email    TEXT,
    password TEXT
);

CREATE TABLE recipe_instructions
(
    id           SERIAL PRIMARY KEY,
    recipe_id    INT,
    instructions JSON,
    CONSTRAINT FK_recipe_id FOREIGN KEY (recipe_id) REFERENCES recipes (id)
);

/* INSERT INTO */
INSERT INTO recipes (meal_type, user_id, name, image, ingredients, macros)
VALUES ('L',
        1,
        'Testy type',
        'TestImage2',
        ARRAY [
            ROW ('Pineapple',44, 123, 74, 95, 31)::ingredient,
            ROW ('Grenade', 160, 6, 33, 5, 1)::ingredient
            ],
        (3072, 97, 33, 77)::recipe_macros);

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

INSERT INTO recipes (meal_type, name, image, ingredients, macros, user_id)
VALUES ('D', name)

/* ALTER TABLE */
ALTER TABLE recipe_instructions
    ADD recipe_id INT;
ALTER TABLE recipe_instructions
    ADD CONSTRAINT FK_recipe_id FOREIGN KEY (recipe_id) REFERENCES recipes (id);

/* SELECT */
-- "WHERE 2 = ANY(id)" checks if the value "2" is an element in id.
SELECT *
FROM users
WHERE 2 = ANY (id);

SELECT id, meal_type, name, image, ingredients, macros, user_id FROM recipes;

SELECT id, unnest(ingredients) FROM recipes;

SELECT ingredient.name,
       ingredient.grams,
       ingredient.calories_pr_hectogram,
       ingredient.carbs_pr_hectogram,
       ingredient.protein_pr_hectogram,
       ingredient.fat_pr_hectogram
FROM recipes,
     unnest(ingredients) as ingredient
WHERE recipes.id = 1;

-- Return true or false, depending on if something exists.
SELECT EXISTS (SELECT 1
               FROM users
               WHERE username = 'Test');

-- Allows us to select everything from users, based on the recipe id they have stored in their recipe_ids array.
SELECT *
FROM users,
     unnest(
             (SELECT recipe_ids FROM users)
     ) AS recipe_id
WHERE recipe_id = 1;

/* UPDATE */
-- Inserts element with the value 2.
UPDATE users
SET recipe_ids = array_append(recipe_ids, 2)
WHERE id = 1;

-- Will remove element with the value 7.
UPDATE users
SET recipe_ids = array_remove(recipe_ids, 7)
WHERE id = 1;

SELECT *
FROM unnest(
                 (SELECT recipe_ids FROM users)
     ) AS recipe_id
WHERE recipe_id = 1;

SELECT *
FROM ingredients
ORDER BY id;

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

/* UPDATE */
UPDATE recipes
SET ingredients = array_append(ingredients, ROW ('Pineapple',44, 123, 74, 95, 31, 0.4)::ingredient)
WHERE id = 3;

UPDATE recipes
SET macros = (4, 3, 2, 1)::recipe_macros
WHERE id IN (SELECT COUNT(*) FROM recipes);

UPDATE recipes
SET macros = (4, 3, 2, 1)::recipe_macros
WHERE id = 8;

/* OTHER */
-- These three queries resets the id order correctly. Remember to delete the temporary table.
CREATE TEMP TABLE temp_recipes AS
SELECT *, ROW_NUMBER() OVER (ORDER BY id) as new_id
FROM recipes;
UPDATE recipes
SET id = temp_recipes.new_id
FROM temp_recipes
WHERE recipes.id = temp_recipes.id;
SELECT setval('recipes_id_seq', (SELECT MAX(id) FROM recipes));

-- Shows information about the connections of a database.
SELECT count(*)                                 AS total_connections,
       count(*) FILTER (WHERE state = 'active') AS active_connections,
       count(*) FILTER (WHERE state = 'idle')   AS idle_connections
FROM pg_stat_activity;