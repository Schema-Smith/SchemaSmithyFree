DROP FUNCTION IF EXISTS `get_customer_balance`;
DELIMITER //
CREATE FUNCTION `get_customer_balance` (p_customer_id int,p_effective_date datetime)
  RETURNS decimal(5,2)
  LANGUAGE SQL
  DETERMINISTIC
  READS SQL DATA
  SQL SECURITY DEFINER
BEGIN

       
       
       
       
       
       

  DECLARE v_rentfees DECIMAL(5,2); 
  DECLARE v_overfees INTEGER;      
  DECLARE v_payments DECIMAL(5,2); 

  SELECT IFNULL(SUM(film.rental_rate),0) INTO v_rentfees
    FROM film, inventory, rental
    WHERE film.film_id = inventory.film_id
      AND inventory.inventory_id = rental.inventory_id
      AND rental.rental_date <= p_effective_date
      AND rental.customer_id = p_customer_id;

  SELECT IFNULL(SUM(IF((TO_DAYS(rental.return_date) - TO_DAYS(rental.rental_date)) > film.rental_duration,
        ((TO_DAYS(rental.return_date) - TO_DAYS(rental.rental_date)) - film.rental_duration),0)),0) INTO v_overfees
    FROM rental, inventory, film
    WHERE film.film_id = inventory.film_id
      AND inventory.inventory_id = rental.inventory_id
      AND rental.rental_date <= p_effective_date
      AND rental.customer_id = p_customer_id;


  SELECT IFNULL(SUM(payment.amount),0) INTO v_payments
    FROM payment

    WHERE payment.payment_date <= p_effective_date
    AND payment.customer_id = p_customer_id;

  RETURN v_rentfees + v_overfees - v_payments;
END //
DELIMITER ;