SET @json_data = '{{city.tabledata}}';

INSERT INTO `sakila`.`city` (`city_id`, `city`, `country_id`, `last_update`)
SELECT `city_id`, `city`, `country_id`, `last_update`
FROM JSON_TABLE(
  @json_data,
  '$[*]' COLUMNS (
    `city_id` INT PATH '$.city_id',
    `city` VARCHAR(50) PATH '$.city',
    `country_id` INT PATH '$.country_id',
    `last_update` TIMESTAMP PATH '$.last_update'
  )
) AS jt
ON DUPLICATE KEY UPDATE
  `city` = VALUES(`city`),
  `country_id` = VALUES(`country_id`),
  `last_update` = VALUES(`last_update`);
