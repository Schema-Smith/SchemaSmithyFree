SET @json_data = '{{payment.tabledata}}';

INSERT INTO `sakila`.`payment` (`payment_id`, `customer_id`, `staff_id`, `rental_id`, `amount`, `payment_date`, `last_update`)
SELECT `payment_id`, `customer_id`, `staff_id`, `rental_id`, `amount`, `payment_date`, `last_update`
FROM JSON_TABLE(
  @json_data,
  '$[*]' COLUMNS (
    `payment_id` INT PATH '$.payment_id',
    `customer_id` INT PATH '$.customer_id',
    `staff_id` INT PATH '$.staff_id',
    `rental_id` INT PATH '$.rental_id',
    `amount` DECIMAL(5,2) PATH '$.amount',
    `payment_date` DATETIME PATH '$.payment_date',
    `last_update` TIMESTAMP PATH '$.last_update'
  )
) AS jt
ON DUPLICATE KEY UPDATE
  `customer_id` = VALUES(`customer_id`),
  `staff_id` = VALUES(`staff_id`),
  `rental_id` = VALUES(`rental_id`),
  `amount` = VALUES(`amount`),
  `payment_date` = VALUES(`payment_date`),
  `last_update` = VALUES(`last_update`);
