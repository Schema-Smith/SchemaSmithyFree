SET @json_data = '{{film_category.tabledata}}';

INSERT INTO `sakila`.`film_category` (`film_id`, `category_id`, `last_update`)
SELECT `film_id`, `category_id`, `last_update`
FROM JSON_TABLE(
  @json_data,
  '$[*]' COLUMNS (
    `film_id` INT PATH '$.film_id',
    `category_id` INT PATH '$.category_id',
    `last_update` TIMESTAMP PATH '$.last_update'
  )
) AS jt
ON DUPLICATE KEY UPDATE
  `last_update` = VALUES(`last_update`);
