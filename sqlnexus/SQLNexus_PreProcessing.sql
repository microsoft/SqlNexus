select 'Nexus preprocessing scripts running' [message]

go

if object_id ('tblNexusInfo') is null
begin
	create table dbo.tblNexusInfo (Attribute nvarchar (200) unique, Value nvarchar(2048))
end
