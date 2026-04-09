SET @json_data = '{{country.tabledata}}';

INSERT INTO `sakila`.`country` (`country_id`, `country`, `last_update`)
SELECT `country_id`, `country`, `last_update`
FROM JSON_TABLE(
  @json_data,
  '$[*]' COLUMNS (
    `country_id` INT PATH '$.country_id',
    `country` VARCHAR(50) PATH '$.country',
    `last_update` TIMESTAMP PATH '$.last_update'
  )
) AS jt
ON DUPLICATE KEY UPDATE
  `country` = VALUES(`country`),
  `last_update` = VALUES(`last_update`);
