SET @json_data = '{{film_actor.tabledata}}';

INSERT INTO `sakila`.`film_actor` (`actor_id`, `film_id`, `last_update`)
SELECT `actor_id`, `film_id`, `last_update`
FROM JSON_TABLE(
  @json_data,
  '$[*]' COLUMNS (
    `actor_id` INT PATH '$.actor_id',
    `film_id` INT PATH '$.film_id',
    `last_update` TIMESTAMP PATH '$.last_update'
  )
) AS jt
ON DUPLICATE KEY UPDATE
  `last_update` = VALUES(`last_update`);
