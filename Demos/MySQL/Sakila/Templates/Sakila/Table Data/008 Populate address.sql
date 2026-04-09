SET @json_data = '{{address.tabledata}}';

INSERT INTO `sakila`.`address` (`address_id`, `address`, `address2`, `district`, `city_id`, `postal_code`, `phone`, `last_update`)
SELECT `address_id`, `address`, `address2`, `district`, `city_id`, `postal_code`, `phone`, `last_update`
FROM JSON_TABLE(
  @json_data,
  '$[*]' COLUMNS (
    `address_id` INT PATH '$.address_id',
    `address` VARCHAR(50) PATH '$.address',
    `address2` VARCHAR(50) PATH '$.address2',
    `district` VARCHAR(20) PATH '$.district',
    `city_id` INT PATH '$.city_id',
    `postal_code` VARCHAR(10) PATH '$.postal_code',
    `phone` VARCHAR(20) PATH '$.phone',
    `last_update` TIMESTAMP PATH '$.last_update'
  )
) AS jt
ON DUPLICATE KEY UPDATE
  `address` = VALUES(`address`),
  `address2` = VALUES(`address2`),
  `district` = VALUES(`district`),
  `city_id` = VALUES(`city_id`),
  `postal_code` = VALUES(`postal_code`),
  `phone` = VALUES(`phone`),
  `last_update` = VALUES(`last_update`);
