SET @json_data = '{{language.tabledata}}';

INSERT INTO `sakila`.`language` (`language_id`, `name`, `last_update`)
SELECT `language_id`, `name`, `last_update`
FROM JSON_TABLE(
  @json_data,
  '$[*]' COLUMNS (
    `language_id` INT PATH '$.language_id',
    `name` CHAR(20) PATH '$.name',
    `last_update` TIMESTAMP PATH '$.last_update'
  )
) AS jt
ON DUPLICATE KEY UPDATE
  `name` = VALUES(`name`),
  `last_update` = VALUES(`last_update`);
