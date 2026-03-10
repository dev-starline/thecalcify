IF NOT EXISTS (
    SELECT 1
    FROM sys.sequences
    WHERE name = 'UserNameSequence'
      AND schema_id = SCHEMA_ID('dbo')
)
BEGIN
    CREATE SEQUENCE dbo.UserNameSequence
        START WITH 1001
        INCREMENT BY 1
        MINVALUE 1001
        NO MAXVALUE
        CACHE 50;
END