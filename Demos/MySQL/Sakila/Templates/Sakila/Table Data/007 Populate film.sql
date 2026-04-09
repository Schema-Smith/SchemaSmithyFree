SET @json_data = '{{film.tabledata}}';

INSERT INTO `sakila`.`film` (`film_id`, `title`, `description`, `release_year`, `language_id`, `original_language_id`, `rental_duration`, `rental_rate`, `length`, `replacement_cost`, `rating`, `special_features`, `last_update`)
SELECT `film_id`, `title`, `description`, `release_year`, `language_id`, `original_language_id`, `rental_duration`, `rental_rate`, `length`, `replacement_cost`, `rating`, `special_features`, `last_update`
FROM JSON_TABLE(
  @json_data,
  '$[*]' COLUMNS (
    `film_id` INT PATH '$.film_id',
    `title` VARCHAR(255) PATH '$.title',
    `description` TEXT PATH '$.description',
    `release_year` YEAR PATH '$.release_year',
    `language_id` INT PATH '$.language_id',
    `original_language_id` INT PATH '$.original_language_id',
    `rental_duration` TINYINT PATH '$.rental_duration',
    `rental_rate` DECIMAL(4,2) PATH '$.rental_rate',
    `length` SMALLINT PATH '$.length',
    `replacement_cost` DECIMAL(5,2) PATH '$.replacement_cost',
    `rating` enum('G','PG','PG-13','R','NC-17') PATH '$.rating',
    `special_features` set('TRAILERS','COMMENTARIES','DELETED SCENES','BEHIND THE SCENES') PATH '$.special_features',
    `last_update` TIMESTAMP PATH '$.last_update'
  )
) AS jt
ON DUPLICATE KEY UPDATE
  `title` = VALUES(`title`),
  `description` = VALUES(`description`),
  `release_year` = VALUES(`release_year`),
  `language_id` = VALUES(`language_id`),
  `original_language_id` = VALUES(`original_language_id`),
  `rental_duration` = VALUES(`rental_duration`),
  `rental_rate` = VALUES(`rental_rate`),
  `length` = VALUES(`length`),
  `replacement_cost` = VALUES(`replacement_cost`),
  `rating` = VALUES(`rating`),
  `special_features` = VALUES(`special_features`),
  `last_update` = VALUES(`last_update`);
