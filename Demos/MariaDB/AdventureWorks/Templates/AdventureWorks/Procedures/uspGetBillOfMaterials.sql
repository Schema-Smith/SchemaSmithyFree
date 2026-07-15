DROP PROCEDURE IF EXISTS `uspGetBillOfMaterials`;
DELIMITER //
CREATE PROCEDURE `uspGetBillOfMaterials` (IN p_StartProductID int,IN p_CheckDate datetime)
  LANGUAGE SQL
  NOT DETERMINISTIC
  CONTAINS SQL
  SQL SECURITY DEFINER
BEGIN
    
    
    
    WITH RECURSIVE `BOM_cte` (`ProductAssemblyID`, `ComponentID`, `ComponentDesc`, `PerAssemblyQty`, `StandardCost`, `ListPrice`, `BOMLevel`, `RecursionLevel`)
    AS (
        
        SELECT b.`ProductAssemblyID`, b.`ComponentID`, p.`Name`, b.`PerAssemblyQty`, p.`StandardCost`, p.`ListPrice`, b.`BOMLevel`, 0
        FROM `Production_BillOfMaterials` b
            INNER JOIN `Production_Product` p
            ON b.`ComponentID` = p.`ProductID`
        WHERE b.`ProductAssemblyID` = p_StartProductID
            AND p_CheckDate >= b.`StartDate`
            AND p_CheckDate <= COALESCE(b.`EndDate`, p_CheckDate)
        UNION ALL
        
        SELECT b.`ProductAssemblyID`, b.`ComponentID`, p.`Name`, b.`PerAssemblyQty`, p.`StandardCost`, p.`ListPrice`, b.`BOMLevel`, cte.`RecursionLevel` + 1
        FROM `BOM_cte` cte
            INNER JOIN `Production_BillOfMaterials` b
            ON b.`ProductAssemblyID` = cte.`ComponentID`
            INNER JOIN `Production_Product` p
            ON b.`ComponentID` = p.`ProductID`
        WHERE p_CheckDate >= b.`StartDate`
            AND p_CheckDate <= COALESCE(b.`EndDate`, p_CheckDate)
    )
    
    SELECT b.`ProductAssemblyID`, b.`ComponentID`, b.`ComponentDesc`, SUM(b.`PerAssemblyQty`) AS `TotalQuantity`, b.`StandardCost`, b.`ListPrice`, b.`BOMLevel`, b.`RecursionLevel`
    FROM `BOM_cte` b
    GROUP BY b.`ComponentID`, b.`ComponentDesc`, b.`ProductAssemblyID`, b.`BOMLevel`, b.`RecursionLevel`, b.`StandardCost`, b.`ListPrice`
    ORDER BY b.`BOMLevel`, b.`ProductAssemblyID`, b.`ComponentID`;
END //
DELIMITER ;