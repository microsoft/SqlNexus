SELECT 'Nexus preprocessing scripts running' [message]

go

IF OBJECT_ID ('tblNexusInfo') IS NULL
BEGIN
	CREATE TABLE dbo.tblNexusInfo (Attribute NVARCHAR (200) UNIQUE, Value NVARCHAR(2048))
END
