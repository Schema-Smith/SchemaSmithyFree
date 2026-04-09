SET @json_data = '{{rental.tabledata}}';

INSERT INTO `sakila`.`rental` (`rental_id`, `rental_date`, `inventory_id`, `customer_id`, `return_date`, `staff_id`, `last_update`)
SELECT `rental_id`, `rental_date`, `inventory_id`, `customer_id`, `return_date`, `staff_id`, `last_update`
FROM JSON_TABLE(
  @json_data,
  '$[*]' COLUMNS (
    `rental_id` INT PATH '$.rental_id',
    `rental_date` DATETIME PATH '$.rental_date',
    `inventory_id` INT PATH '$.inventory_id',
    `customer_id` INT PATH '$.customer_id',
    `return_date` DATETIME PATH '$.return_date',
    `staff_id` INT PATH '$.staff_id',
    `last_update` TIMESTAMP PATH '$.last_update'
  )
) AS jt
ON DUPLICATE KEY UPDATE
  `rental_date` = VALUES(`rental_date`),
  `inventory_id` = VALUES(`inventory_id`),
  `customer_id` = VALUES(`customer_id`),
  `return_date` = VALUES(`return_date`),
  `staff_id` = VALUES(`staff_id`),
  `last_update` = VALUES(`last_update`);
