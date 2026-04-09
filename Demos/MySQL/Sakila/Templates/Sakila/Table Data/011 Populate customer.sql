SET @json_data = '{{customer.tabledata}}';

INSERT INTO `sakila`.`customer` (`customer_id`, `store_id`, `first_name`, `last_name`, `email`, `address_id`, `active`, `create_date`, `last_update`)
SELECT `customer_id`, `store_id`, `first_name`, `last_name`, `email`, `address_id`, `active`, `create_date`, `last_update`
FROM JSON_TABLE(
  @json_data,
  '$[*]' COLUMNS (
    `customer_id` INT PATH '$.customer_id',
    `store_id` INT PATH '$.store_id',
    `first_name` VARCHAR(45) PATH '$.first_name',
    `last_name` VARCHAR(45) PATH '$.last_name',
    `email` VARCHAR(50) PATH '$.email',
    `address_id` INT PATH '$.address_id',
    `active` TINYINT PATH '$.active',
    `create_date` DATETIME PATH '$.create_date',
    `last_update` TIMESTAMP PATH '$.last_update'
  )
) AS jt
ON DUPLICATE KEY UPDATE
  `store_id` = VALUES(`store_id`),
  `first_name` = VALUES(`first_name`),
  `last_name` = VALUES(`last_name`),
  `email` = VALUES(`email`),
  `address_id` = VALUES(`address_id`),
  `active` = VALUES(`active`),
  `create_date` = VALUES(`create_date`),
  `last_update` = VALUES(`last_update`);
