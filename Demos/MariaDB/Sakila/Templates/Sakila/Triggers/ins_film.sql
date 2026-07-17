DROP TRIGGER IF EXISTS `ins_film`;
DELIMITER //
CREATE TRIGGER `ins_film`
  AFTER INSERT
  ON `film` 
  FOR EACH ROW 
BEGIN
BEGIN
BEGIN
    INSERT INTO film_text (film_id, title, description)
        VALUES (new.film_id, new.title, new.description);
  END;
END;
END //
DELIMITER ;