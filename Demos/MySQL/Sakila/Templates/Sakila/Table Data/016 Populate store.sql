SET @json_data = '{{store.tabledata}}';

INSERT INTO `sakila`.`store` (`store_id`, `manager_staff_id`, `address_id`, `last_update`)
SELECT `store_id`, `manager_staff_id`, `address_id`, `last_update`
FROM JSON_TABLE(
  @json_data,
  '$[*]' COLUMNS (
    `store_id` INT PATH '$.store_id',
    `manager_staff_id` INT PATH '$.manager_staff_id',
    `address_id` INT PATH '$.address_id',
    `last_update` TIMESTAMP PATH '$.last_update'
  )
) AS jt
ON DUPLICATE KEY UPDATE
  `manager_staff_id` = VALUES(`manager_staff_id`),
  `address_id` = VALUES(`address_id`),
  `last_update` = VALUES(`last_update`);
