SET @json_data = '{{category.tabledata}}';

INSERT INTO `sakila`.`category` (`category_id`, `name`, `last_update`)
SELECT `category_id`, `name`, `last_update`
FROM JSON_TABLE(
  @json_data,
  '$[*]' COLUMNS (
    `category_id` INT PATH '$.category_id',
    `name` VARCHAR(25) PATH '$.name',
    `last_update` TIMESTAMP PATH '$.last_update'
  )
) AS jt
ON DUPLICATE KEY UPDATE
  `name` = VALUES(`name`),
  `last_update` = VALUES(`last_update`);
