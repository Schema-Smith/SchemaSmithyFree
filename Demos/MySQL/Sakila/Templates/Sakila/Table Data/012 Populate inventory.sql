SET @json_data = '{{inventory.tabledata}}';

INSERT INTO `sakila`.`inventory` (`inventory_id`, `film_id`, `store_id`, `last_update`)
SELECT `inventory_id`, `film_id`, `store_id`, `last_update`
FROM JSON_TABLE(
  @json_data,
  '$[*]' COLUMNS (
    `inventory_id` INT PATH '$.inventory_id',
    `film_id` INT PATH '$.film_id',
    `store_id` INT PATH '$.store_id',
    `last_update` TIMESTAMP PATH '$.last_update'
  )
) AS jt
ON DUPLICATE KEY UPDATE
  `film_id` = VALUES(`film_id`),
  `store_id` = VALUES(`store_id`),
  `last_update` = VALUES(`last_update`);
