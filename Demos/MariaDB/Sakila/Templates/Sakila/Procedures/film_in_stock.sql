DROP PROCEDURE IF EXISTS `film_in_stock`;
DELIMITER //
CREATE PROCEDURE `film_in_stock` (IN p_film_id int,IN p_store_id int,OUT p_film_count int)
  LANGUAGE SQL
  NOT DETERMINISTIC
  READS SQL DATA
  SQL SECURITY DEFINER
BEGIN
     SELECT inventory_id
     FROM inventory
     WHERE film_id = p_film_id
     AND store_id = p_store_id
     AND inventory_in_stock(inventory_id);

     SELECT FOUND_ROWS() INTO p_film_count;
END //
DELIMITER ;