SET @json_data = '{{actor.tabledata}}';

INSERT INTO `sakila`.`actor` (`actor_id`, `first_name`, `last_name`, `last_update`)
SELECT `actor_id`, `first_name`, `last_name`, `last_update`
FROM JSON_TABLE(
  @json_data,
  '$[*]' COLUMNS (
    `actor_id` INT PATH '$.actor_id',
    `first_name` VARCHAR(45) PATH '$.first_name',
    `last_name` VARCHAR(45) PATH '$.last_name',
    `last_update` TIMESTAMP PATH '$.last_update'
  )
) AS jt
ON DUPLICATE KEY UPDATE
  `first_name` = VALUES(`first_name`),
  `last_name` = VALUES(`last_name`),
  `last_update` = VALUES(`last_update`);
