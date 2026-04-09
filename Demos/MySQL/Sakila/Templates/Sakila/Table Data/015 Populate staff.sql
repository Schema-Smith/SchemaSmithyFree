SET @json_data = '{{staff.tabledata}}';

INSERT INTO `sakila`.`staff` (`staff_id`, `first_name`, `last_name`, `address_id`, `picture`, `email`, `store_id`, `active`, `username`, `password`, `last_update`)
SELECT `staff_id`, `first_name`, `last_name`, `address_id`, FROM_BASE64(`picture`), `email`, `store_id`, `active`, `username`, `password`, `last_update`
FROM JSON_TABLE(
  @json_data,
  '$[*]' COLUMNS (
    `staff_id` INT PATH '$.staff_id',
    `first_name` VARCHAR(45) PATH '$.first_name',
    `last_name` VARCHAR(45) PATH '$.last_name',
    `address_id` INT PATH '$.address_id',
    `picture` TEXT PATH '$.picture',
    `email` VARCHAR(50) PATH '$.email',
    `store_id` INT PATH '$.store_id',
    `active` TINYINT PATH '$.active',
    `username` VARCHAR(16) PATH '$.username',
    `password` VARCHAR(40) PATH '$.password',
    `last_update` TIMESTAMP PATH '$.last_update'
  )
) AS jt
ON DUPLICATE KEY UPDATE
  `first_name` = VALUES(`first_name`),
  `last_name` = VALUES(`last_name`),
  `address_id` = VALUES(`address_id`),
  `picture` = VALUES(`picture`),
  `email` = VALUES(`email`),
  `store_id` = VALUES(`store_id`),
  `active` = VALUES(`active`),
  `username` = VALUES(`username`),
  `password` = VALUES(`password`),
  `last_update` = VALUES(`last_update`);
