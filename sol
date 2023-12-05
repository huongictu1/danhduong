CREATE PROC [dbo].[Report2ALL](
    @Month AS INT,
    @Year AS INT,
    @Dept AS INT,
    @Stock AS INT
)
AS
BEGIN
    -- Calculate values once
    DECLARE @LastMonthStockValue DECIMAL(18, 3);
    DECLARE @SumInputValue DECIMAL(18, 3);
    DECLARE @SumOutputValue DECIMAL(18, 3);

    SET @LastMonthStockValue = dbo.LastMonthStock(c.Barcode, @Month, @Year);
    SET @SumInputValue = dbo.SumInput(c.Barcode, @Month, @Year);
    SET @SumOutputValue = dbo.SumOutput(c.Barcode, @Month, @Year);

    SELECT ROW_NUMBER() OVER (ORDER BY t.Barcode) AS No, t.*
    FROM (
        SELECT
            c.ChemicalName,
            c.ProduceCode,
            d.Name AS Dept,
            c.Barcode,
            c.ControlNo,
            u.Name AS Unit,
            @LastMonthStockValue AS LastMonthStock,
            @SumInputValue AS TotalInput,
            @SumOutputValue AS TotalOutput,
            dbo.GetActual(c.Barcode, @Month, @Year) AS ActualInventory,
            dbo.MinIsZero((@LastMonthStockValue + @SumInputValue) - @SumOutputValue) AS CalculationStock,
            ROUND(dbo.GetActual(c.Barcode, @Month, @Year) - (@LastMonthStockValue + @SumInputValue - @SumOutputValue), 3) AS Differed,
            dbo.Get_Reason(c.Barcode, @Month, @Year) AS Reason,
            dbo.GetSmaxALL2(c.Barcode, @Month, @Year) AS STT,
            dbo.GetStatus(c.Barcode, @Month, @Year) AS [Status],
            CONVERT(VARCHAR, dbo.GetDateApproval(c.Barcode, @Month, @Year), 121) AS DateApproval,
            dbo.GetPICConfirm(c.Barcode, @Month, @Year) AS Pic,
            d.ID,
            cs.StockID
        FROM [dbo].[ChemicalStore] c
            LEFT JOIN ChemicalList cs ON c.Barcode = cs.Barcode
            LEFT JOIN Dept d ON c.Dept = d.ID 
            LEFT JOIN Unit u ON c.Unit = u.ID
        WHERE 
            (MONTH(DateCurrent) = @Month AND YEAR(DateCurrent) = @Year)
            AND (
                d.ID = (CASE WHEN @Dept = -1 THEN d.ID ELSE @Dept END)
                AND cs.StockID = (CASE WHEN @Stock = -1 THEN cs.StockID ELSE @Stock END)
            )
        GROUP BY c.Barcode, c.ChemicalName, c.ProduceCode, d.Name, c.ControlNo, u.Name, d.ID, cs.StockID

        UNION ALL

        SELECT 
            c.ChemicalName,
            c.ProduceCode,
            d.Name AS Dept,
            c.Barcode,
            c.ControlNo,
            u.Name AS Unit,
            @LastMonthStockValue AS LastMonthStock,
            TotalInput = 0,
            TotalOutput = 0,
            dbo.GetActual(c.Barcode, @Month, @Year) AS ActualInventory,
            @LastMonthStockValue AS CalculationStock,
            dbo.GetActual(c.Barcode, @Month, @Year) - @LastMonthStockValue AS Differed,
            dbo.Get_Reason(c.Barcode, @Month, @Year) AS Reason,
            dbo.GetSmaxALL2(c.Barcode, @Month, @Year) AS STT,
            dbo.GetStatus(c.Barcode, @Month, @Year) AS [Status],
            CONVERT(VARCHAR, dbo.GetDateApproval(c.Barcode, @Month, @Year), 121) AS DateApproval,
            dbo.GetPICConfirm(c.Barcode, @Month, @Year) AS Pic,
            d.ID,
            cs.StockID
        FROM ChemicalList c 
            LEFT JOIN ChemicalList cs ON c.Barcode = cs.Barcode
            LEFT JOIN Dept d ON c.Dept = d.ID 
            LEFT JOIN Unit u ON c.Unit = u.ID
        WHERE 
            c.Barcode NOT IN (
                SELECT Barcode 
                FROM ChemicalStore 
                WHERE 
                    (MONTH(DateCurrent) = @Month AND YEAR(DateCurrent) = @Year)
                    AND (
                        d.ID = (CASE WHEN @Dept = -1 THEN d.ID ELSE @Dept END)
                        AND cs.StockID = (CASE WHEN @Stock = -1 THEN cs.StockID ELSE @Stock END)
                    )
            )
            AND @LastMonthStockValue <> 0
            AND (
                d.ID = (CASE WHEN @Dept = -1 THEN d.ID ELSE @Dept END)
                AND cs.StockID = (CASE WHEN @Stock = -1 THEN cs.StockID ELSE @Stock END)
            )
    ) AS t;

END
