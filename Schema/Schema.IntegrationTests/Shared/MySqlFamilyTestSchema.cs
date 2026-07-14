// Copyright (c) SchemaSmith Contributors. Licensed under the SSCL v2.0.

using System.Data;

namespace Schema.IntegrationTests.Shared;

/// <summary>
/// Sakila-mirroring test schema + seed data used by both the MySQL and MariaDb integration
/// fixtures. The DDL is standard MySQL-family syntax that MariaDB accepts unchanged, so a single
/// builder serves both engines and keeps the two fixtures thin.
/// </summary>
public static class MySqlFamilyTestSchema
{
    public static void Create(IDbCommand cmd, string dbName)
    {
        // language table (referenced by film)
        cmd.CommandText = $@"
            CREATE TABLE `{dbName}`.`language` (
                language_id TINYINT UNSIGNED NOT NULL AUTO_INCREMENT,
                name CHAR(20) NOT NULL,
                last_update TIMESTAMP NOT NULL DEFAULT CURRENT_TIMESTAMP ON UPDATE CURRENT_TIMESTAMP,
                PRIMARY KEY (language_id)
            ) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4";
        cmd.ExecuteNonQuery();

        cmd.CommandText = $@"
            INSERT INTO `{dbName}`.`language` (name) VALUES
            ('English'), ('Italian'), ('Japanese'), ('Mandarin'), ('French'), ('German')";
        cmd.ExecuteNonQuery();

        // actor table
        cmd.CommandText = $@"
            CREATE TABLE `{dbName}`.`actor` (
                actor_id SMALLINT UNSIGNED NOT NULL AUTO_INCREMENT,
                first_name VARCHAR(45) NOT NULL,
                last_name VARCHAR(45) NOT NULL,
                last_update TIMESTAMP NOT NULL DEFAULT CURRENT_TIMESTAMP ON UPDATE CURRENT_TIMESTAMP,
                PRIMARY KEY (actor_id),
                KEY idx_actor_last_name (last_name)
            ) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4";
        cmd.ExecuteNonQuery();

        cmd.CommandText = $@"
            INSERT INTO `{dbName}`.`actor` (first_name, last_name) VALUES
            ('PENELOPE', 'GUINESS'),
            ('NICK', 'WAHLBERG'),
            ('ED', 'CHASE'),
            ('JENNIFER', 'DAVIS'),
            ('JOHNNY', 'LOLLOBRIGIDA')";
        cmd.ExecuteNonQuery();

        // country table
        cmd.CommandText = $@"
            CREATE TABLE `{dbName}`.`country` (
                country_id SMALLINT UNSIGNED NOT NULL AUTO_INCREMENT,
                country VARCHAR(50) NOT NULL,
                last_update TIMESTAMP NOT NULL DEFAULT CURRENT_TIMESTAMP ON UPDATE CURRENT_TIMESTAMP,
                PRIMARY KEY (country_id)
            ) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4";
        cmd.ExecuteNonQuery();

        cmd.CommandText = $@"
            INSERT INTO `{dbName}`.`country` (country) VALUES
            ('Afghanistan'), ('Algeria'), ('American Samoa'), ('Angola'), ('Argentina')";
        cmd.ExecuteNonQuery();

        // city table (FK to country)
        cmd.CommandText = $@"
            CREATE TABLE `{dbName}`.`city` (
                city_id SMALLINT UNSIGNED NOT NULL AUTO_INCREMENT,
                city VARCHAR(50) NOT NULL,
                country_id SMALLINT UNSIGNED NOT NULL,
                last_update TIMESTAMP NOT NULL DEFAULT CURRENT_TIMESTAMP ON UPDATE CURRENT_TIMESTAMP,
                PRIMARY KEY (city_id),
                KEY idx_fk_country_id (country_id),
                CONSTRAINT fk_city_country FOREIGN KEY (country_id) REFERENCES `country` (country_id) ON DELETE RESTRICT ON UPDATE CASCADE
            ) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4";
        cmd.ExecuteNonQuery();

        cmd.CommandText = $@"
            INSERT INTO `{dbName}`.`city` (city, country_id) VALUES
            ('Kabul', 1), ('Batna', 2), ('Tafuna', 3), ('Benguela', 4), ('Córdoba', 5)";
        cmd.ExecuteNonQuery();

        // film table (FK to language)
        cmd.CommandText = $@"
            CREATE TABLE `{dbName}`.`film` (
                film_id SMALLINT UNSIGNED NOT NULL AUTO_INCREMENT,
                title VARCHAR(255) NOT NULL,
                description TEXT,
                release_year YEAR,
                language_id TINYINT UNSIGNED NOT NULL,
                original_language_id TINYINT UNSIGNED DEFAULT NULL,
                rental_duration TINYINT UNSIGNED NOT NULL DEFAULT 3,
                rental_rate DECIMAL(4,2) NOT NULL DEFAULT 4.99,
                length SMALLINT UNSIGNED DEFAULT NULL,
                replacement_cost DECIMAL(5,2) NOT NULL DEFAULT 19.99,
                rating ENUM('G','PG','PG-13','R','NC-17') DEFAULT 'G',
                special_features SET('Trailers','Commentaries','Deleted Scenes','Behind the Scenes') DEFAULT NULL,
                last_update TIMESTAMP NOT NULL DEFAULT CURRENT_TIMESTAMP ON UPDATE CURRENT_TIMESTAMP,
                PRIMARY KEY (film_id),
                KEY idx_title (title(255)),
                KEY idx_fk_language_id (language_id),
                KEY idx_fk_original_language_id (original_language_id),
                CONSTRAINT fk_film_language FOREIGN KEY (language_id) REFERENCES `language` (language_id) ON DELETE RESTRICT ON UPDATE CASCADE,
                CONSTRAINT fk_film_language_original FOREIGN KEY (original_language_id) REFERENCES `language` (language_id) ON DELETE RESTRICT ON UPDATE CASCADE
            ) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4";
        cmd.ExecuteNonQuery();

        cmd.CommandText = $@"
            INSERT INTO `{dbName}`.`film` (title, description, release_year, language_id, rental_duration, rental_rate, length, replacement_cost, rating) VALUES
            ('ACADEMY DINOSAUR', 'A Epic Drama of a Feminist And a Mad Scientist', 2006, 1, 6, 0.99, 86, 20.99, 'PG'),
            ('ACE GOLDFINGER', 'A Astounding Epistle of a Database Administrator', 2006, 1, 3, 4.99, 48, 12.99, 'G'),
            ('ADAPTATION HOLES', 'A Astounding Reflection of a Lumberjack', 2006, 1, 7, 2.99, 50, 18.99, 'NC-17')";
        cmd.ExecuteNonQuery();

        // film_text table (with fulltext index)
        cmd.CommandText = $@"
            CREATE TABLE `{dbName}`.`film_text` (
                film_id SMALLINT NOT NULL,
                title VARCHAR(255) NOT NULL,
                description TEXT,
                PRIMARY KEY (film_id),
                FULLTEXT KEY idx_title_description (title, description)
            ) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4";
        cmd.ExecuteNonQuery();

        cmd.CommandText = $@"
            INSERT INTO `{dbName}`.`film_text` VALUES
            (1, 'ACADEMY DINOSAUR', 'A Epic Drama of a Feminist And a Mad Scientist'),
            (2, 'ACE GOLDFINGER', 'A Astounding Epistle of a Database Administrator'),
            (3, 'ADAPTATION HOLES', 'A Astounding Reflection of a Lumberjack')";
        cmd.ExecuteNonQuery();

        // category table
        cmd.CommandText = $@"
            CREATE TABLE `{dbName}`.`category` (
                category_id TINYINT UNSIGNED NOT NULL AUTO_INCREMENT,
                name VARCHAR(25) NOT NULL,
                last_update TIMESTAMP NOT NULL DEFAULT CURRENT_TIMESTAMP ON UPDATE CURRENT_TIMESTAMP,
                PRIMARY KEY (category_id)
            ) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4";
        cmd.ExecuteNonQuery();

        cmd.CommandText = $@"
            INSERT INTO `{dbName}`.`category` (name) VALUES
            ('Action'), ('Animation'), ('Children'), ('Classics'), ('Comedy')";
        cmd.ExecuteNonQuery();

        // film_category table (composite key)
        cmd.CommandText = $@"
            CREATE TABLE `{dbName}`.`film_category` (
                film_id SMALLINT UNSIGNED NOT NULL,
                category_id TINYINT UNSIGNED NOT NULL,
                last_update TIMESTAMP NOT NULL DEFAULT CURRENT_TIMESTAMP ON UPDATE CURRENT_TIMESTAMP,
                PRIMARY KEY (film_id, category_id),
                KEY fk_film_category_category (category_id),
                CONSTRAINT fk_film_category_film FOREIGN KEY (film_id) REFERENCES `film` (film_id) ON DELETE RESTRICT ON UPDATE CASCADE,
                CONSTRAINT fk_film_category_category FOREIGN KEY (category_id) REFERENCES `category` (category_id) ON DELETE RESTRICT ON UPDATE CASCADE
            ) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4";
        cmd.ExecuteNonQuery();

        cmd.CommandText = $@"
            INSERT INTO `{dbName}`.`film_category` VALUES
            (1, 1, CURRENT_TIMESTAMP), (1, 2, CURRENT_TIMESTAMP), (2, 1, CURRENT_TIMESTAMP), (3, 3, CURRENT_TIMESTAMP)";
        cmd.ExecuteNonQuery();

        // film_actor table (composite key)
        cmd.CommandText = $@"
            CREATE TABLE `{dbName}`.`film_actor` (
                actor_id SMALLINT UNSIGNED NOT NULL,
                film_id SMALLINT UNSIGNED NOT NULL,
                last_update TIMESTAMP NOT NULL DEFAULT CURRENT_TIMESTAMP ON UPDATE CURRENT_TIMESTAMP,
                PRIMARY KEY (actor_id, film_id),
                KEY idx_fk_film_id (film_id),
                CONSTRAINT fk_film_actor_actor FOREIGN KEY (actor_id) REFERENCES `actor` (actor_id) ON DELETE RESTRICT ON UPDATE CASCADE,
                CONSTRAINT fk_film_actor_film FOREIGN KEY (film_id) REFERENCES `film` (film_id) ON DELETE RESTRICT ON UPDATE CASCADE
            ) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4";
        cmd.ExecuteNonQuery();

        cmd.CommandText = $@"
            INSERT INTO `{dbName}`.`film_actor` VALUES
            (1, 1, CURRENT_TIMESTAMP), (1, 2, CURRENT_TIMESTAMP), (2, 1, CURRENT_TIMESTAMP),
            (2, 3, CURRENT_TIMESTAMP), (3, 2, CURRENT_TIMESTAMP)";
        cmd.ExecuteNonQuery();

        // staff table (with blob for picture)
        cmd.CommandText = $@"
            CREATE TABLE `{dbName}`.`staff` (
                staff_id TINYINT UNSIGNED NOT NULL AUTO_INCREMENT,
                first_name VARCHAR(45) NOT NULL,
                last_name VARCHAR(45) NOT NULL,
                email VARCHAR(50) DEFAULT NULL,
                active TINYINT(1) NOT NULL DEFAULT 1,
                username VARCHAR(16) NOT NULL,
                password VARCHAR(40) DEFAULT NULL,
                picture BLOB DEFAULT NULL,
                last_update TIMESTAMP NOT NULL DEFAULT CURRENT_TIMESTAMP ON UPDATE CURRENT_TIMESTAMP,
                PRIMARY KEY (staff_id)
            ) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4";
        cmd.ExecuteNonQuery();

        cmd.CommandText = $@"
            INSERT INTO `{dbName}`.`staff` (first_name, last_name, email, active, username) VALUES
            ('Mike', 'Hillyer', 'Mike.Hillyer@test.com', 1, 'Mike'),
            ('Jon', 'Stephens', 'Jon.Stephens@test.com', 1, 'Jon')";
        cmd.ExecuteNonQuery();

        // customer table
        cmd.CommandText = $@"
            CREATE TABLE `{dbName}`.`customer` (
                customer_id SMALLINT UNSIGNED NOT NULL AUTO_INCREMENT,
                first_name VARCHAR(45) NOT NULL,
                last_name VARCHAR(45) NOT NULL,
                email VARCHAR(50) DEFAULT NULL,
                active TINYINT(1) NOT NULL DEFAULT 1,
                create_date DATETIME NOT NULL,
                last_update TIMESTAMP DEFAULT CURRENT_TIMESTAMP ON UPDATE CURRENT_TIMESTAMP,
                PRIMARY KEY (customer_id),
                KEY idx_last_name (last_name)
            ) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4";
        cmd.ExecuteNonQuery();

        cmd.CommandText = $@"
            INSERT INTO `{dbName}`.`customer` (first_name, last_name, email, create_date) VALUES
            ('MARY', 'SMITH', 'MARY.SMITH@test.org', NOW()),
            ('PATRICIA', 'JOHNSON', 'PATRICIA.JOHNSON@test.org', NOW()),
            ('LINDA', 'WILLIAMS', 'LINDA.WILLIAMS@test.org', NOW())";
        cmd.ExecuteNonQuery();

        // inventory table
        cmd.CommandText = $@"
            CREATE TABLE `{dbName}`.`inventory` (
                inventory_id MEDIUMINT UNSIGNED NOT NULL AUTO_INCREMENT,
                film_id SMALLINT UNSIGNED NOT NULL,
                last_update TIMESTAMP NOT NULL DEFAULT CURRENT_TIMESTAMP ON UPDATE CURRENT_TIMESTAMP,
                PRIMARY KEY (inventory_id),
                KEY idx_fk_film_id (film_id),
                CONSTRAINT fk_inventory_film FOREIGN KEY (film_id) REFERENCES `film` (film_id) ON DELETE RESTRICT ON UPDATE CASCADE
            ) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4";
        cmd.ExecuteNonQuery();

        cmd.CommandText = $@"
            INSERT INTO `{dbName}`.`inventory` (film_id) VALUES (1), (1), (2), (2), (3)";
        cmd.ExecuteNonQuery();

        // rental table (with FKs and nullable return_date)
        cmd.CommandText = $@"
            CREATE TABLE `{dbName}`.`rental` (
                rental_id INT NOT NULL AUTO_INCREMENT,
                rental_date DATETIME NOT NULL,
                inventory_id MEDIUMINT UNSIGNED NOT NULL,
                customer_id SMALLINT UNSIGNED NOT NULL,
                return_date DATETIME DEFAULT NULL,
                staff_id TINYINT UNSIGNED NOT NULL,
                last_update TIMESTAMP NOT NULL DEFAULT CURRENT_TIMESTAMP ON UPDATE CURRENT_TIMESTAMP,
                PRIMARY KEY (rental_id),
                UNIQUE KEY idx_rental_uq (rental_date, inventory_id, customer_id),
                KEY idx_fk_inventory_id (inventory_id),
                KEY idx_fk_customer_id (customer_id),
                KEY idx_fk_staff_id (staff_id),
                CONSTRAINT fk_rental_inventory FOREIGN KEY (inventory_id) REFERENCES `inventory` (inventory_id) ON DELETE RESTRICT ON UPDATE CASCADE,
                CONSTRAINT fk_rental_customer FOREIGN KEY (customer_id) REFERENCES `customer` (customer_id) ON DELETE RESTRICT ON UPDATE CASCADE,
                CONSTRAINT fk_rental_staff FOREIGN KEY (staff_id) REFERENCES `staff` (staff_id) ON DELETE RESTRICT ON UPDATE CASCADE
            ) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4";
        cmd.ExecuteNonQuery();

        cmd.CommandText = $@"
            INSERT INTO `{dbName}`.`rental` (rental_date, inventory_id, customer_id, return_date, staff_id) VALUES
            ('2024-01-01 10:00:00', 1, 1, '2024-01-08 10:00:00', 1),
            ('2024-01-02 11:00:00', 2, 2, '2024-01-09 11:00:00', 1),
            ('2024-01-03 12:00:00', 3, 3, NULL, 2),
            ('2024-01-04 13:00:00', 4, 1, NULL, 2),
            ('2024-01-05 14:00:00', 5, 2, '2024-01-12 14:00:00', 1)";
        cmd.ExecuteNonQuery();

        // payment table (with decimal amount)
        cmd.CommandText = $@"
            CREATE TABLE `{dbName}`.`payment` (
                payment_id SMALLINT UNSIGNED NOT NULL AUTO_INCREMENT,
                customer_id SMALLINT UNSIGNED NOT NULL,
                staff_id TINYINT UNSIGNED NOT NULL,
                rental_id INT DEFAULT NULL,
                amount DECIMAL(5,2) NOT NULL,
                payment_date DATETIME NOT NULL,
                last_update TIMESTAMP DEFAULT CURRENT_TIMESTAMP ON UPDATE CURRENT_TIMESTAMP,
                PRIMARY KEY (payment_id),
                KEY idx_fk_staff_id (staff_id),
                KEY idx_fk_customer_id (customer_id),
                KEY fk_payment_rental (rental_id),
                CONSTRAINT fk_payment_customer FOREIGN KEY (customer_id) REFERENCES `customer` (customer_id) ON DELETE RESTRICT ON UPDATE CASCADE,
                CONSTRAINT fk_payment_staff FOREIGN KEY (staff_id) REFERENCES `staff` (staff_id) ON DELETE RESTRICT ON UPDATE CASCADE,
                CONSTRAINT fk_payment_rental FOREIGN KEY (rental_id) REFERENCES `rental` (rental_id) ON DELETE SET NULL ON UPDATE CASCADE
            ) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4";
        cmd.ExecuteNonQuery();

        cmd.CommandText = $@"
            INSERT INTO `{dbName}`.`payment` (customer_id, staff_id, rental_id, amount, payment_date) VALUES
            (1, 1, 1, 2.99, '2024-01-01 10:00:00'),
            (2, 1, 2, 4.99, '2024-01-02 11:00:00'),
            (3, 2, 3, 1.99, '2024-01-03 12:00:00'),
            (1, 2, 4, 3.99, '2024-01-04 13:00:00'),
            (2, 1, 5, 5.99, '2024-01-05 14:00:00')";
        cmd.ExecuteNonQuery();
    }
}
