﻿SET NOCOUNT ON;

--add extra columns that represent local server time, computed based on offset if the data is available
--these will facilitation joins between ReadTrace.* tables and other tbl_* tables - the latter storing datetime in local server time 

IF (OBJECT_ID('[ReadTrace].[tblBatches]') IS NOT NULL)
BEGIN
    --add columns to tblBatches
    IF (
           (COLUMNPROPERTY(OBJECT_ID('[ReadTrace].[tblBatches]'), 'StartTime_local', 'ColumnId') IS NULL)
           AND (COLUMNPROPERTY(OBJECT_ID('[ReadTrace].[tblBatches]'), 'EndTime_local', 'ColumnId') IS NULL)
       )
    BEGIN
        ALTER TABLE [ReadTrace].[tblBatches]
        ADD StartTime_local DATETIME,
            EndTime_local DATETIME;
    END;
END;

IF (OBJECT_ID('[ReadTrace].[tblStatements]') IS NOT NULL)
BEGIN
    --add columns to tblStatements
    IF (
           (COLUMNPROPERTY(OBJECT_ID('[ReadTrace].[tblStatements]'), 'StartTime_local', 'ColumnId') IS NULL)
           AND (COLUMNPROPERTY(OBJECT_ID('[ReadTrace].[tblStatements]'), 'EndTime_local', 'ColumnId') IS NULL)
       )
    BEGIN
        ALTER TABLE [ReadTrace].[tblStatements]
        ADD StartTime_local DATETIME,
            EndTime_local DATETIME;
    END;
END;

IF (OBJECT_ID('[ReadTrace].[tblConnections]') IS NOT NULL)
BEGIN
    --add columns to tblConnections
    IF (
           (COLUMNPROPERTY(OBJECT_ID('[ReadTrace].[tblConnections]'), 'StartTime_local', 'ColumnId') IS NULL)
           AND (COLUMNPROPERTY(OBJECT_ID('[ReadTrace].[tblConnections]'), 'EndTime_local', 'ColumnId') IS NULL)
       )
    BEGIN
        ALTER TABLE [ReadTrace].[tblConnections]
        ADD StartTime_local DATETIME,
            EndTime_local DATETIME;
    END;
END;

GO
IF (
       (OBJECT_ID('tbl_ServerProperties') IS NOT NULL)
       OR (OBJECT_ID('tbl_server_times') IS NOT NULL)
   )
BEGIN

    --get the offset from one of two possible tables
    DECLARE @utc_to_local_offset NUMERIC(3, 0) = 0;

    IF OBJECT_ID('tbl_ServerProperties') IS NOT NULL
    BEGIN
        SELECT @utc_to_local_offset = PropertyValue
        FROM dbo.tbl_ServerProperties
        WHERE PropertyName = 'UTCOffset_in_Hours';
    END;
    ELSE IF OBJECT_ID('tbl_server_times') IS NOT NULL
    BEGIN
        SELECT TOP 1
               @utc_to_local_offset = time_delta_hours * -1
        FROM dbo.tbl_server_times;
    END;
    --update the new columns in tblBatches with local times
    IF (
           (COLUMNPROPERTY(OBJECT_ID('[ReadTrace].[tblBatches]'), 'StartTime_local', 'ColumnId') IS NOT NULL)
           AND (COLUMNPROPERTY(OBJECT_ID('[ReadTrace].[tblBatches]'), 'EndTime_local', 'ColumnId') IS NOT NULL)
       )
    BEGIN
        UPDATE [ReadTrace].[tblBatches]
        SET StartTime_local = DATEADD(HOUR, @utc_to_local_offset, StartTime),
            EndTime_local = DATEADD(HOUR, @utc_to_local_offset, EndTime);
    END;

    --update the new columns in tblStatements with local times
    IF (
           (COLUMNPROPERTY(OBJECT_ID('[ReadTrace].[tblStatements]'), 'StartTime_local', 'ColumnId') IS NOT NULL)
           AND (COLUMNPROPERTY(OBJECT_ID('[ReadTrace].[tblStatements]'), 'EndTime_local', 'ColumnId') IS NOT NULL)
       )
    BEGIN
        UPDATE [ReadTrace].[tblStatements]
        SET StartTime_local = DATEADD(HOUR, @utc_to_local_offset, StartTime),
            EndTime_local = DATEADD(HOUR, @utc_to_local_offset, EndTime);
    END;


    --update the new columns in tblConnections with local times
    IF (
           (COLUMNPROPERTY(OBJECT_ID('[ReadTrace].[tblConnections]'), 'StartTime_local', 'ColumnId') IS NOT NULL)
           AND (COLUMNPROPERTY(OBJECT_ID('[ReadTrace].[tblConnections]'), 'EndTime_local', 'ColumnId') IS NOT NULL)
       )
    BEGIN

        UPDATE [ReadTrace].[tblConnections]
        SET StartTime_local = DATEADD(HOUR, @utc_to_local_offset, StartTime),
            EndTime_local = DATEADD(HOUR, @utc_to_local_offset, EndTime);
    END;

END;
GO

--format the tasklist imported table

IF (OBJECT_ID('tbl_ActiveProcesses_OS') IS NOT NULL)
BEGIN
    ALTER TABLE dbo.tbl_ActiveProcesses_OS ADD MemUsage_MB DECIMAL(10, 3);
END;
GO

IF (OBJECT_ID('tbl_ActiveProcesses_OS') IS NOT NULL)
BEGIN
    BEGIN TRY
        UPDATE dbo.tbl_ActiveProcesses_OS
        SET MemUsage_MB = CONVERT(
                                     DECIMAL(10, 3),
                                     CONVERT(DECIMAL(10, 3), REPLACE(REPLACE([Mem Usage], ' K', ''), ',', '')) / 1024
                                 );
    END TRY
    BEGIN CATCH
        SELECT ERROR_NUMBER() AS ErrorNumber,
               ERROR_SEVERITY() AS ErrorSeverity,
               ERROR_STATE() AS ErrorState,
               ERROR_LINE() AS ErrorLine,
               ERROR_MESSAGE() AS ErrorMessage;
    END CATCH;
END;

GO
--clean up the Systeminfo table after import
IF ((OBJECT_ID('tbl_SystemInformation') IS NOT NULL))
BEGIN
    BEGIN TRY
        UPDATE dbo.tbl_SystemInformation
        SET Property = REPLACE(Property, ':', '');
    END TRY
    BEGIN CATCH
        SELECT ERROR_NUMBER() AS ErrorNumber,
               ERROR_SEVERITY() AS ErrorSeverity,
               ERROR_STATE() AS ErrorState,
               ERROR_LINE() AS ErrorLine,
               ERROR_MESSAGE() AS ErrorMessage;
    END CATCH;
END;

--create a filter drivers table
IF OBJECT_ID('dbo.filter_driver_altitudes') IS NOT NULL
    DROP TABLE dbo.filter_driver_altitudes;
GO
CREATE TABLE dbo.filter_driver_altitudes
(
    FilterType NVARCHAR(48),
    Minifilter NVARCHAR(64),
    Altitude BIGINT,
    Company NVARCHAR(128)
);
GO
GO
BEGIN TRAN;
INSERT INTO dbo.filter_driver_altitudes
VALUES
('Kernel', 'ntoskrnl.exe', 425500, 'Microsoft');
INSERT INTO dbo.filter_driver_altitudes
VALUES
('Kernel', 'ntoskrnl.exe', 425000, 'Microsoft');
INSERT INTO dbo.filter_driver_altitudes
VALUES
('Top', 'wcnfs.sys', 409900, 'Microsoft');
INSERT INTO dbo.filter_driver_altitudes
VALUES
('Top', 'bindflt.sys', 409800, 'Microsoft');
INSERT INTO dbo.filter_driver_altitudes
VALUES
('Top', 'cldflt.sys', 409500, 'Microsoft');
INSERT INTO dbo.filter_driver_altitudes
VALUES
('Top', 'iorate.sys', 409010, 'Microsoft');
INSERT INTO dbo.filter_driver_altitudes
VALUES
('Top', 'ioqos.sys', 409000, 'Microsoft');
INSERT INTO dbo.filter_driver_altitudes
VALUES
('Top', 'fsdepends.sys', 407000, 'Microsoft');
INSERT INTO dbo.filter_driver_altitudes
VALUES
('Top', 'sftredir.sys', 406000, 'Microsoft');
INSERT INTO dbo.filter_driver_altitudes
VALUES
('Top', 'dfs.sys', 405000, 'Microsoft');
INSERT INTO dbo.filter_driver_altitudes
VALUES
('Top', 'IntelEgDriver.sys', 404950.5, 'Intel Corp');
INSERT INTO dbo.filter_driver_altitudes
VALUES
('Top', 'VeeamFCT.sys', 404920, 'Veeam Software');
INSERT INTO dbo.filter_driver_altitudes
VALUES
('Top', 'sek.sys', 404915.5, 'Sentry Corporation');
INSERT INTO dbo.filter_driver_altitudes
VALUES
('Top', 'tracker.sys', 404910, 'Acronis');
INSERT INTO dbo.filter_driver_altitudes
VALUES
('Top', 'csvnsflt.sys', 404900, 'Microsoft');
INSERT INTO dbo.filter_driver_altitudes
VALUES
('Top', 'csvflt.sys', 404800, 'Microsoft');
INSERT INTO dbo.filter_driver_altitudes
VALUES
('Top', 'Microsoft.Uev.AgentDriver.sys', 404710, 'Microsoft');
INSERT INTO dbo.filter_driver_altitudes
VALUES
('Top', 'AppvVfs.sys', 404700, 'Microsoft');
INSERT INTO dbo.filter_driver_altitudes
VALUES
('Top', 'CCFFilter.sys', 404600, 'Microsoft');
INSERT INTO dbo.filter_driver_altitudes
VALUES
('Top', 'FwDI.sys', 402130.5, 'First Watch Limited');
INSERT INTO dbo.filter_driver_altitudes
VALUES
('Top', '360AntiSteal.sys', 402120.5, '360 Software (Beijing)');
INSERT INTO dbo.filter_driver_altitudes
VALUES
('Top', 'uberAgentDrv.sys', 402110, 'vast limits GmbH');
INSERT INTO dbo.filter_driver_altitudes
VALUES
('Top', 'mrigflt.sys', 402100, 'Paramount Software Ltd');
INSERT INTO dbo.filter_driver_altitudes
VALUES
('Top', 'darkscope-drv.sys', 402030.5, 'Zhuhai YiZhiSec co');
INSERT INTO dbo.filter_driver_altitudes
VALUES
('Top', 'XCOAmon.sys', 402025.5, 'TRIART INC');
INSERT INTO dbo.filter_driver_altitudes
VALUES
('Top', 'RevoNetDriver.sys', 402020, 'J''s Communication Co.');
INSERT INTO dbo.filter_driver_altitudes
VALUES
('Top', 'dciogrd.sys', 402010, 'Datacloak Tech');
INSERT INTO dbo.filter_driver_altitudes
VALUES
('Top', 'Dewdrv.sys', 402000, 'Dell Technologies');
INSERT INTO dbo.filter_driver_altitudes
VALUES
('Top', 'zsusbstorfilt.sys', 401910, 'Zshield Inc');
INSERT INTO dbo.filter_driver_altitudes
VALUES
('Top', 'eaw.sys', 401900, 'Raytheon Cyber Solutions');
INSERT INTO dbo.filter_driver_altitudes
VALUES
('Top', 'TVFsfilter.sys', 401800, 'TrustView');
INSERT INTO dbo.filter_driver_altitudes
VALUES
('Top', 'KKDiskProtecter.sys', 401700, 'Goldmsg');
INSERT INTO dbo.filter_driver_altitudes
VALUES
('Top', 'AgentComm.sys', 401600, 'Trustwave Holding Inc');
INSERT INTO dbo.filter_driver_altitudes
VALUES
('Top', 'rvsavd.sys', 401500, 'CJSC Returnil Software');
INSERT INTO dbo.filter_driver_altitudes
VALUES
('Top', 'DGMinFlt.sys', 401410, 'Digital Guardian Inc.');
INSERT INTO dbo.filter_driver_altitudes
VALUES
('Top', 'dgdmk.sys', 401400, 'Verdasys Inc.');
INSERT INTO dbo.filter_driver_altitudes
VALUES
('Top', 'stadrv6x64.sys', 401350.5, 'Netskope Inc.');
INSERT INTO dbo.filter_driver_altitudes
VALUES
('Top', 'stadrv6x32.sys', 401350.5, 'Netskope Inc.');
INSERT INTO dbo.filter_driver_altitudes
VALUES
('Top', 'tusbstorfilt.sys', 401300, 'SimplyCore LLC');
INSERT INTO dbo.filter_driver_altitudes
VALUES
('Top', 'pcgenfam.sys', 401200, 'Soluto');
INSERT INTO dbo.filter_driver_altitudes
VALUES
('Top', 'atrsdfw.sys', 401100, 'Altiris');
INSERT INTO dbo.filter_driver_altitudes
VALUES
('Top', 'tpfilter.sys', 401000, 'RedPhone Security');
INSERT INTO dbo.filter_driver_altitudes
VALUES
('Top', 'MBIG2Prot.sys', 400920, 'Malwarebytes Inc');
INSERT INTO dbo.filter_driver_altitudes
VALUES
('Top', 'mbamwatchdog.sys', 400900, 'Malwarebytes Corporation');
INSERT INTO dbo.filter_driver_altitudes
VALUES
('Top', 'DSESafeCtrlDrv.sys', 400803, 'Shanghai Eff-Soft IT');
INSERT INTO dbo.filter_driver_altitudes
VALUES
('Top', 'edevmonm.sys', 400800.3, 'ESET spol. s r.o.');
INSERT INTO dbo.filter_driver_altitudes
VALUES
('Top', 'edevmon.sys', 400800, 'ESET spol. s r.o.');
INSERT INTO dbo.filter_driver_altitudes
VALUES
('Top', 'vmwprotect.sys', 400700.5, 'VMware, Inc.');
INSERT INTO dbo.filter_driver_altitudes
VALUES
('Top', 'vmwcdrfilter.sys', 400700.3, 'VMware, Inc.');
INSERT INTO dbo.filter_driver_altitudes
VALUES
('Top', 'vmwflstor.sys', 400700, 'VMware, Inc.');
INSERT INTO dbo.filter_driver_altitudes
VALUES
('Top', 'TsQBDrv.sys', 400600, 'Tencent Technology');
INSERT INTO dbo.filter_driver_altitudes
VALUES
('Top', 'PolyPortFlt.sys', 400490, 'PolyPort Inc');
INSERT INTO dbo.filter_driver_altitudes
VALUES
('Top', 'Dscdriver.sys', 400300, 'Dell Technologies Inc.');
INSERT INTO dbo.filter_driver_altitudes
VALUES
('Activity Monitor', 'zd_mon.sys', 389520.50, 'Zecurion');
INSERT INTO dbo.filter_driver_altitudes
VALUES
('Activity Monitor', 'icrlmonitor.sys', 389518.50, 'Delta Electronics Inc');
INSERT INTO dbo.filter_driver_altitudes
VALUES
('Activity Monitor', 'klboot.sys', 389510.00, 'Kaspersky Lab');
INSERT INTO dbo.filter_driver_altitudes
VALUES
('Activity Monitor', 'klfdefsf.sys', 389500.00, 'Kaspersky Lab');
INSERT INTO dbo.filter_driver_altitudes
VALUES
('Activity Monitor', 'JKMCPF.sys', 389492.70, 'JiranData Co. Ltd');
INSERT INTO dbo.filter_driver_altitudes
VALUES
('Activity Monitor', 'JDEDRPF.sys', 389492.50, 'JiranData Co. Ltd');
INSERT INTO dbo.filter_driver_altitudes
VALUES
('Activity Monitor', 'JDPPWF.sys', 389492.00, 'JiranData Co. Ltd');
INSERT INTO dbo.filter_driver_altitudes
VALUES
('Activity Monitor', 'JDPPSF.sys', 389490.00, 'JiranData Co. Ltd');
INSERT INTO dbo.filter_driver_altitudes
VALUES
('Activity Monitor', 'FFDriver.sys', 389470.00, 'ColorTokens');
INSERT INTO dbo.filter_driver_altitudes
VALUES
('Activity Monitor', 'evaccin.sys', 389455.50, 'databps.com');
INSERT INTO dbo.filter_driver_altitudes
VALUES
('Activity Monitor', 'SeRdr.sys', 389450.00, 'rhipe Australia Pty');
INSERT INTO dbo.filter_driver_altitudes
VALUES
('Activity Monitor', 'bw_fssec.sys', 389430.50, 'Wuhan Buwei Software Technology Co.,Ltd');
INSERT INTO dbo.filter_driver_altitudes
VALUES
('Activity Monitor', 'SecurityPro.sys', 389430.30, 'Wuhan Buwei Software Technology Co.,Ltd');
INSERT INTO dbo.filter_driver_altitudes
VALUES
('Activity Monitor', 'defragger.sys', 389420.00, 'Microsoft');
INSERT INTO dbo.filter_driver_altitudes
VALUES
('Activity Monitor', 'storagedrv.sys', 389400.00, 'SMTechnology Co.');
INSERT INTO dbo.filter_driver_altitudes
VALUES
('Activity Monitor', 'NetPeeker.sys', 389330.00, 'eMingSoftware Inc');
INSERT INTO dbo.filter_driver_altitudes
VALUES
('Activity Monitor', 'path8flt.sys', 389320.00, 'Telefónica Digital');
INSERT INTO dbo.filter_driver_altitudes
VALUES
('Activity Monitor', 'DLPDriverNfn.sys', 389310.50, 'Acronis');
INSERT INTO dbo.filter_driver_altitudes
VALUES
('Activity Monitor', 'NgScan.sys', 389310.00, 'Acronis');
INSERT INTO dbo.filter_driver_altitudes
VALUES
('Activity Monitor', 'icrlmonitor.sys', 389300.00, 'Industrial Technology');
INSERT INTO dbo.filter_driver_altitudes
VALUES
('Activity Monitor', 'gibepcore.sys', 389290.00, 'Group-IB LTD');
INSERT INTO dbo.filter_driver_altitudes
VALUES
('Activity Monitor', 'cpflt.sys', 389285.50, 'Cloudplan GmbH');
INSERT INTO dbo.filter_driver_altitudes
VALUES
('Activity Monitor', 'enmon.sys', 389280.00, 'OpenText Corp');
INSERT INTO dbo.filter_driver_altitudes
VALUES
('Activity Monitor', 'wsafefilter.sys', 389272.00, 'WidgetNuri Corp');
INSERT INTO dbo.filter_driver_altitudes
VALUES
('Activity Monitor', 'RansomDetect.sys', 389270.00, 'WidgetNuri Corp');
INSERT INTO dbo.filter_driver_altitudes
VALUES
('Activity Monitor', 'PPLPMFilter.sys', 389265.50, 'PolicyPak Software');
INSERT INTO dbo.filter_driver_altitudes
VALUES
('Activity Monitor', 'cbfsfilter2017.sys', 389260.00, 'Mobile Content Mgmt');
INSERT INTO dbo.filter_driver_altitudes
VALUES
('Activity Monitor', 'cbfilter20.sys', 389251.00, 'SecureLink Inc.');
INSERT INTO dbo.filter_driver_altitudes
VALUES
('Activity Monitor', 'CBFSFilter2017.sys', 389250.00, 'SecureLink Inc.');
INSERT INTO dbo.filter_driver_altitudes
VALUES
('Activity Monitor', 'GmBase.sys', 389248.00, 'NanJing Geomarking');
INSERT INTO dbo.filter_driver_altitudes
VALUES
('Activity Monitor', 'MagicProtect.sys', 389247.00, 'NanJing Geomarking');
INSERT INTO dbo.filter_driver_altitudes
VALUES
('Activity Monitor', 'cbfsfilter2017.sys', 389245.00, 'NanJing Geomarking');
INSERT INTO dbo.filter_driver_altitudes
VALUES
('Activity Monitor', 'cbfsfilter2020.sys', 389245.00, 'NanJing Geomarking');
INSERT INTO dbo.filter_driver_altitudes
VALUES
('Activity Monitor', 'DTDSel.sys', 389242.00, 'DELL Technologies');
INSERT INTO dbo.filter_driver_altitudes
VALUES
('Activity Monitor', 'NWEDriver.sys', 389240.00, 'Dell Technologies');
INSERT INTO dbo.filter_driver_altitudes
VALUES
('Activity Monitor', 'cytmon.sys', 389230.00, 'Cytrence Inc');
INSERT INTO dbo.filter_driver_altitudes
VALUES
('Activity Monitor', 'SophosED.sys', 389220.00, 'Sophos');
INSERT INTO dbo.filter_driver_altitudes
VALUES
('Activity Monitor', 'MonsterK.sys', 389210.00, 'Somma Inc');
INSERT INTO dbo.filter_driver_altitudes
VALUES
('Activity Monitor', 'IFS64.sys', 389200.00, 'Ashampoo Development');
INSERT INTO dbo.filter_driver_altitudes
VALUES
('Activity Monitor', 'TSTFsReDir.sys', 389192.00, 'ThinScale Tech');
INSERT INTO dbo.filter_driver_altitudes
VALUES
('Activity Monitor', 'TSTRegReDir.sys', 389191.00, 'ThinScale Tech');
INSERT INTO dbo.filter_driver_altitudes
VALUES
('Activity Monitor', 'TSTFilter.sys', 389190.00, 'ThinScale Tech');
INSERT INTO dbo.filter_driver_altitudes
VALUES
('Activity Monitor', 'VrnsFilter.sys', 389180.00, 'Varonis Ltd');
INSERT INTO dbo.filter_driver_altitudes
VALUES
('Activity Monitor', 'slb_guard.sys', 389175.00, 'Lenovo Beijing');
INSERT INTO dbo.filter_driver_altitudes
VALUES
('Activity Monitor', 'lrtp.sys', 389170.00, 'Lenovo Beijing');
INSERT INTO dbo.filter_driver_altitudes
VALUES
('Activity Monitor', 'ipcomfltr.sys', 389160.00, 'Bluzen Inc');
INSERT INTO dbo.filter_driver_altitudes
VALUES
('Activity Monitor', 'SvCBT.sys', 389150.00, 'Spharsoft Technologies');
INSERT INTO dbo.filter_driver_altitudes
VALUES
('Activity Monitor', 'mbamshuriken.sys', 389140.00, 'Malwarebytes');
INSERT INTO dbo.filter_driver_altitudes
VALUES
('Activity Monitor', 'FGFLT.sys', 389135.50, 'WinAbility Software');
INSERT INTO dbo.filter_driver_altitudes
VALUES
('Activity Monitor', 'ContainerMonitor.sys', 389130.00, 'Aqua Security');
INSERT INTO dbo.filter_driver_altitudes
VALUES
('Activity Monitor', 'cmflt.sys', 389125.00, 'Certero');
INSERT INTO dbo.filter_driver_altitudes
VALUES
('Activity Monitor', 'SaMFlt.sys', 389120.00, 'DreamCrafts');
INSERT INTO dbo.filter_driver_altitudes
VALUES
('Activity Monitor', 'RuiMinispy.sys', 389117.00, 'RuiGuard Ltd');
INSERT INTO dbo.filter_driver_altitudes
VALUES
('Activity Monitor', 'RuiFileAccess.sys', 389115.00, 'RuiGuard Ltd');
COMMIT TRAN;
GO
BEGIN TRAN;
INSERT INTO dbo.filter_driver_altitudes
VALUES
('Activity Monitor', 'RuiEye.sys', 389113.00, 'RuiGuard Ltd');
INSERT INTO dbo.filter_driver_altitudes
VALUES
('Activity Monitor', 'RuiMachine.sys', 389111.00, 'RuiGuard Ltd');
INSERT INTO dbo.filter_driver_altitudes
VALUES
('Activity Monitor', 'windd.sys', 389110.00, 'Comae Tech');
INSERT INTO dbo.filter_driver_altitudes
VALUES
('Activity Monitor', 'cbfsfilter2017.sys', 389105.00, 'Basein Networks');
INSERT INTO dbo.filter_driver_altitudes
VALUES
('Activity Monitor', 'taobserveflt.sys', 389100.00, 'ThinAir Labs Inc');
INSERT INTO dbo.filter_driver_altitudes
VALUES
('Activity Monitor', 'fsrvlock.sys', 389098.00, 'Man Technology Inc');
INSERT INTO dbo.filter_driver_altitudes
VALUES
('Activity Monitor', 'bsrfsflt.sys', 389096.00, 'Man Technology Inc');
INSERT INTO dbo.filter_driver_altitudes
VALUES
('Activity Monitor', 'fsrfilter.sys', 389094.00, 'Man Technology Inc');
INSERT INTO dbo.filter_driver_altitudes
VALUES
('Activity Monitor', 'vollock.sys', 389092.00, 'Man Technology Inc');
INSERT INTO dbo.filter_driver_altitudes
VALUES
('Activity Monitor', 'drbdlock.sys', 389090.00, 'Man Technology Inc');
INSERT INTO dbo.filter_driver_altitudes
VALUES
('Activity Monitor', 'dcfsgrd.sys', 389085.00, 'Datacloak Tech');
INSERT INTO dbo.filter_driver_altitudes
VALUES
('Activity Monitor', 'hsmltmon.sys', 389080.00, 'Hitachi Solutions');
INSERT INTO dbo.filter_driver_altitudes
VALUES
('Activity Monitor', 'AternityRegistryHook.sys', 389070.00, 'Aternity Ltd');
INSERT INTO dbo.filter_driver_altitudes
VALUES
('Activity Monitor', 'MozyNextFilter.sys', 389068.00, 'Carbonite Inc');
INSERT INTO dbo.filter_driver_altitudes
VALUES
('Activity Monitor', 'MozyCorpFilter.sys', 389067.00, 'Carbonite Inc');
INSERT INTO dbo.filter_driver_altitudes
VALUES
('Activity Monitor', 'MozyEntFilter.sys', 389066.00, 'Carbonite Inc');
INSERT INTO dbo.filter_driver_altitudes
VALUES
('Activity Monitor', 'MozyOEMFilter.sys', 389065.00, 'Carbonite Inc');
INSERT INTO dbo.filter_driver_altitudes
VALUES
('Activity Monitor', 'MozyEnterpriseFilter.sys', 389064.00, 'Carbonite Inc');
INSERT INTO dbo.filter_driver_altitudes
VALUES
('Activity Monitor', 'MozyProFilter.sys', 389063.00, 'Carbonite Inc');
INSERT INTO dbo.filter_driver_altitudes
VALUES
('Activity Monitor', 'MozyHomeFilter.sys', 389062.00, 'Carbonite Inc');
INSERT INTO dbo.filter_driver_altitudes
VALUES
('Activity Monitor', 'BDSFilter.sys', 389061.00, 'Carbonite Inc');
INSERT INTO dbo.filter_driver_altitudes
VALUES
('Activity Monitor', 'CSBFilter.sys', 389060.00, 'Carbonite Inc');
INSERT INTO dbo.filter_driver_altitudes
VALUES
('Activity Monitor', 'f_pmf.sys', 389055.50, 'Fasoo Inc.');
INSERT INTO dbo.filter_driver_altitudes
VALUES
('Activity Monitor', 'ChemometecFilter.sys', 389050.00, 'ChemoMetec');
INSERT INTO dbo.filter_driver_altitudes
VALUES
('Activity Monitor', 'bcloudsafe.sys', 389045.50, 'AISHU Technology Corp');
INSERT INTO dbo.filter_driver_altitudes
VALUES
('Activity Monitor', 'SentinelMonitor.sys', 389040.00, 'SentinelOne');
INSERT INTO dbo.filter_driver_altitudes
VALUES
('Activity Monitor', 'DhWatchdog.sys', 389030.00, 'Microsoft');
INSERT INTO dbo.filter_driver_altitudes
VALUES
('Activity Monitor', 'edrsensor.sys', 389025.00, 'Bitdefender SRL');
INSERT INTO dbo.filter_driver_altitudes
VALUES
('Activity Monitor', 'bdprivmon.sys', 389022.00, 'Bitdefender SRL');
INSERT INTO dbo.filter_driver_altitudes
VALUES
('Activity Monitor', 'NpEtw.sys', 389020.00, 'Koby Kahane');
INSERT INTO dbo.filter_driver_altitudes
VALUES
('Activity Monitor', 'OczMiniFilter.sys', 389010.00, 'OCZ Storage');
INSERT INTO dbo.filter_driver_altitudes
VALUES
('Activity Monitor', 'ielcp.sys', 389004.00, 'Intel Corporation');
INSERT INTO dbo.filter_driver_altitudes
VALUES
('Activity Monitor', 'IESlp.sys', 389002.00, 'Intel Corporation');
INSERT INTO dbo.filter_driver_altitudes
VALUES
('Activity Monitor', 'IntelCAS.sys', 389000.00, 'Intel Corporation');
INSERT INTO dbo.filter_driver_altitudes
VALUES
('Activity Monitor', 'boxifier.sys', 388990.00, 'Kenubi');
INSERT INTO dbo.filter_driver_altitudes
VALUES
('Activity Monitor', 'SamsungRapidFSFltr.sys', 388980.00, 'NVELO Inc.');
INSERT INTO dbo.filter_driver_altitudes
VALUES
('Activity Monitor', 'drsfile.sys', 388970.00, 'MRY Inc.');
INSERT INTO dbo.filter_driver_altitudes
VALUES
('Activity Monitor', 'CbFltFs4.sys', 388966.00, 'Simopro Technology');
INSERT INTO dbo.filter_driver_altitudes
VALUES
('Activity Monitor', 'CrUnCopy.sys', 388964.00, 'Shenzhen CloudRiver');
INSERT INTO dbo.filter_driver_altitudes
VALUES
('Activity Monitor', 'aictracedrv_am.sys', 388960.00, 'AI Consulting');
INSERT INTO dbo.filter_driver_altitudes
VALUES
('Activity Monitor', 'fiopolicyfilter.sys', 388954.00, 'SanDisk Inc.');
INSERT INTO dbo.filter_driver_altitudes
VALUES
('Activity Monitor', 'sodatpfl.sys', 388951.00, 'SODATSW spol. s r.o.');
INSERT INTO dbo.filter_driver_altitudes
VALUES
('Activity Monitor', 'sodatpfl.sys', 388950.20, 'SODATSW');
INSERT INTO dbo.filter_driver_altitudes
VALUES
('Activity Monitor', 'fcontrol.sys', 388950.00, 'SODATSW spol. s r.o.');
INSERT INTO dbo.filter_driver_altitudes
VALUES
('Activity Monitor', 'qfilter.sys', 388940.00, 'Quorum Labs');
INSERT INTO dbo.filter_driver_altitudes
VALUES
('Activity Monitor', 'Redlight.sys', 388930.00, 'Trustware Ltd');
INSERT INTO dbo.filter_driver_altitudes
VALUES
('Activity Monitor', 'ClumioChangeBlockMf.sys', 388925.00, 'Clumio Inc');
INSERT INTO dbo.filter_driver_altitudes
VALUES
('Activity Monitor', 'eps.sys', 388920.00, 'Lumension');
INSERT INTO dbo.filter_driver_altitudes
VALUES
('Activity Monitor', 'VHDTrack.sys', 388915.00, 'Intronis Inc');
INSERT INTO dbo.filter_driver_altitudes
VALUES
('Activity Monitor', 'VHDDelta.sys', 388912.00, 'Niriva LLC');
INSERT INTO dbo.filter_driver_altitudes
VALUES
('Activity Monitor', 'FSTrace.sys', 388910.00, 'Niriva LLC');
INSERT INTO dbo.filter_driver_altitudes
VALUES
('Activity Monitor', 'YahooStorage.sys', 388900.00, 'Yahoo Japan Corporation');
INSERT INTO dbo.filter_driver_altitudes
VALUES
('Activity Monitor', 'KeWF.sys', 388890.00, 'KEBA AG');
INSERT INTO dbo.filter_driver_altitudes
VALUES
('Activity Monitor', 'epregflt.sys', 388888.00, 'Check Point Software');
INSERT INTO dbo.filter_driver_altitudes
VALUES
('Activity Monitor', 'epklib.sys', 388886.00, 'Check Point Software');
INSERT INTO dbo.filter_driver_altitudes
VALUES
('Activity Monitor', 'zsfprt.sys', 388880.00, 'k4solution Co., Ltd.');
INSERT INTO dbo.filter_driver_altitudes
VALUES
('Activity Monitor', 'dsflt.sys', 388876.00, 'cEncrypt');
INSERT INTO dbo.filter_driver_altitudes
VALUES
('Activity Monitor', 'bfaccess.sys', 388872.00, 'bitFence Inc.');
INSERT INTO dbo.filter_driver_altitudes
VALUES
('Activity Monitor', 'xcpl.sys', 388870.00, 'X-Cloud Systems');
INSERT INTO dbo.filter_driver_altitudes
VALUES
('Activity Monitor', 'DRMFilter.sys', 388867.50, 'ManageEngine Zoho');
INSERT INTO dbo.filter_driver_altitudes
VALUES
('Activity Monitor', 'DFMFilter.sys', 388867.00, 'ManageEngine Zoho');
INSERT INTO dbo.filter_driver_altitudes
VALUES
('Activity Monitor', 'DCFAFilter.sys', 388866.00, 'ManageEngine Zoho');
INSERT INTO dbo.filter_driver_altitudes
VALUES
('Activity Monitor', 'RMPHVMonitor.sys', 388865.00, 'ManageEngine Zoho');
INSERT INTO dbo.filter_driver_altitudes
VALUES
('Activity Monitor', 'FAPMonitor.sys', 388864.00, 'ManageEngine Zoho');
INSERT INTO dbo.filter_driver_altitudes
VALUES
('Activity Monitor', 'FACEDrv.sys', 388863.50, 'ManageEngine Zoho');
INSERT INTO dbo.filter_driver_altitudes
VALUES
('Activity Monitor', 'MEARWFltDriver.sys', 388863.00, 'ManageEngine Zoho');
INSERT INTO dbo.filter_driver_altitudes
VALUES
('Activity Monitor', 'SerMonDriver.sys', 388862.50, 'ManageEngine Zoho');
INSERT INTO dbo.filter_driver_altitudes
VALUES
('Activity Monitor', 'EaseFlt.sys', 388860.00, 'EaseVault Technologies Inc.');
INSERT INTO dbo.filter_driver_altitudes
VALUES
('Activity Monitor', 'rpwatcher.sys', 388855.00, 'Best Security');
INSERT INTO dbo.filter_driver_altitudes
VALUES
('Activity Monitor', 'sieflt.sys', 388852.00, 'Quick Heal Technologies Pvt. Ltd.');
INSERT INTO dbo.filter_driver_altitudes
VALUES
('Activity Monitor', 'cssdlp.sys', 388851.00, 'Quick Heal Technologies Pvt. Ltd.');
INSERT INTO dbo.filter_driver_altitudes
VALUES
('Activity Monitor', 'cssdlp.sys', 388850.00, 'CoSoSys');
INSERT INTO dbo.filter_driver_altitudes
VALUES
('Activity Monitor', 'INISBDrv64.sys', 388840.00, 'Initech Inc.');
INSERT INTO dbo.filter_driver_altitudes
VALUES
('Activity Monitor', 'kconv.sys', 388832.00, 'Fitsec Ltd');
INSERT INTO dbo.filter_driver_altitudes
VALUES
('Activity Monitor', 'trace.sys', 388831.00, 'Fitsec Ltd');
INSERT INTO dbo.filter_driver_altitudes
VALUES
('Activity Monitor', 'SandDriver.sys', 388830.00, 'Fitsec Ltd');
INSERT INTO dbo.filter_driver_altitudes
VALUES
('Activity Monitor', 'dskmn.sys', 388820.00, 'Honeycomb Technologies');
INSERT INTO dbo.filter_driver_altitudes
VALUES
('Activity Monitor', 'offsm.sys', 388811.00, 'Jiransoft Co., Ltd');
INSERT INTO dbo.filter_driver_altitudes
VALUES
('Activity Monitor', 'xkfsfd.sys', 388810.00, 'Jiransoft Co., Ltd');
INSERT INTO dbo.filter_driver_altitudes
VALUES
('Activity Monitor', 'JKPPOB.sys', 388808.00, 'Jiransoft Co., Ltd');
INSERT INTO dbo.filter_driver_altitudes
VALUES
('Activity Monitor', 'JKPPXK.sys', 388807.00, 'Jiransoft Co., Ltd');
INSERT INTO dbo.filter_driver_altitudes
VALUES
('Activity Monitor', 'JKPPPF.sys', 388806.00, 'Jiransoft Co., Ltd');
INSERT INTO dbo.filter_driver_altitudes
VALUES
('Activity Monitor', 'JKPPOK.sys', 388805.00, 'Jiransoft Co., Ltd');
INSERT INTO dbo.filter_driver_altitudes
VALUES
('Activity Monitor', 'pcpifd.sys', 388800.00, 'Jiransoft Co., Ltd');
INSERT INTO dbo.filter_driver_altitudes
VALUES
('Activity Monitor', 'NNTInfo.sys', 388790.00, 'New Net Technologies Limited');
INSERT INTO dbo.filter_driver_altitudes
VALUES
('Activity Monitor', 'NmpFilter.sys', 388781.00, 'IBM');
INSERT INTO dbo.filter_driver_altitudes
VALUES
('Activity Monitor', 'FsMonitor.sys', 388780.00, 'IBM');
INSERT INTO dbo.filter_driver_altitudes
VALUES
('Activity Monitor', 'CVCBT.sys', 388770.00, 'CommVault Systems, Inc.');
INSERT INTO dbo.filter_driver_altitudes
VALUES
('Activity Monitor', 'AwareCore.sys', 388760.00, 'TaaSera Inc.');
INSERT INTO dbo.filter_driver_altitudes
VALUES
('Activity Monitor', 'laFS.sys', 388750.00, 'NetworkProfi Ltd');
INSERT INTO dbo.filter_driver_altitudes
VALUES
('Activity Monitor', 'fsnk.sys', 388740.00, 'SoftPerfect Research');
INSERT INTO dbo.filter_driver_altitudes
VALUES
('Activity Monitor', 'RGNT.sys', 388730.00, 'HFN Inc.');
INSERT INTO dbo.filter_driver_altitudes
VALUES
('Activity Monitor', 'fltRs329.sys', 388720.00, 'Secured Globe Inc.');
INSERT INTO dbo.filter_driver_altitudes
VALUES
('Activity Monitor', 'ospmon.sys', 388710.00, 'SC ODEKIN SOLUTIONS SRL');
INSERT INTO dbo.filter_driver_altitudes
VALUES
('Activity Monitor', 'edsigk.sys', 388700.00, 'Enterprise Data Solutions, Inc.');
INSERT INTO dbo.filter_driver_altitudes
VALUES
('Activity Monitor', 'fiometer.sys', 388692.00, 'Fusion-io');
INSERT INTO dbo.filter_driver_altitudes
VALUES
('Activity Monitor', 'dcSnapRestore.sys', 388690.00, 'Fusion-io');
INSERT INTO dbo.filter_driver_altitudes
VALUES
('Activity Monitor', 'SytSelfProtect.sys', 388688.50, 'Sunyata Electronic Tech');
INSERT INTO dbo.filter_driver_altitudes
VALUES
('Activity Monitor', 'fam.sys', 388680.00, 'Quick Heal Technologies Pvt. Ltd.');
INSERT INTO dbo.filter_driver_altitudes
VALUES
('Activity Monitor', 'vidderfs.sys', 388675.00, 'Vidder Inc.');
INSERT INTO dbo.filter_driver_altitudes
VALUES
('Activity Monitor', 'Tritiumfltr.sys', 388670.00, 'Tritium Inc.');
INSERT INTO dbo.filter_driver_altitudes
VALUES
('Activity Monitor', 'HexisFSMonitor.sys', 388660.00, 'Hexis Cyber Solutions');
INSERT INTO dbo.filter_driver_altitudes
VALUES
('Activity Monitor', 'BlackbirdFSA.sys', 388650.00, 'BeyondTrust Inc.');
INSERT INTO dbo.filter_driver_altitudes
VALUES
('Activity Monitor', 'TMUMS.sys', 388642.00, 'Trend Micro Inc.');
INSERT INTO dbo.filter_driver_altitudes
VALUES
('Activity Monitor', 'hfileflt.sys', 388640.00, 'Trend Micro Inc.');
INSERT INTO dbo.filter_driver_altitudes
VALUES
('Activity Monitor', 'TMUMH.sys', 388630.00, 'Trend Micro Inc.');
INSERT INTO dbo.filter_driver_altitudes
VALUES
('Activity Monitor', 'AcDriver.sys', 388620.00, 'Trend Micro, Inc.');
INSERT INTO dbo.filter_driver_altitudes
VALUES
('Activity Monitor', 'SakFile.sys', 388610.00, 'Trend Micro, Inc.');
INSERT INTO dbo.filter_driver_altitudes
VALUES
('Activity Monitor', 'SakMFile.sys', 388600.00, 'Trend Micro, Inc.');
INSERT INTO dbo.filter_driver_altitudes
VALUES
('Activity Monitor', 'rsfdrv.sys', 388580.00, 'Clonix Co');
INSERT INTO dbo.filter_driver_altitudes
VALUES
('Activity Monitor', 'alcapture.sys', 388570.00, 'Airlock Digital Pty Ltd');
INSERT INTO dbo.filter_driver_altitudes
VALUES
('Activity Monitor', 'kmNWCH.sys', 388560.00, 'IGLOO SECURITY, Inc.');
INSERT INTO dbo.filter_driver_altitudes
VALUES
('Activity Monitor', 'ISIRMFmon.sys', 388550.00, 'ALPS SYSTEM INTERGRATION CO., LTD');
INSERT INTO dbo.filter_driver_altitudes
VALUES
('Activity Monitor', 'EsProbe.sys', 388542.00, 'Stormshield');
INSERT INTO dbo.filter_driver_altitudes
VALUES
('Activity Monitor', 'heimdall.sys', 388540.00, 'Arkoon Network Security');
INSERT INTO dbo.filter_driver_altitudes
VALUES
('Activity Monitor', 'thetta.sys', 388532.00, 'Syncopate');
INSERT INTO dbo.filter_driver_altitudes
VALUES
('Activity Monitor', 'thetta.sys', 388531.00, 'Syncopate');
INSERT INTO dbo.filter_driver_altitudes
VALUES
('Activity Monitor', 'thetta.sys', 388530.00, 'Syncopate');
COMMIT TRAN;
GO
BEGIN TRAN;
INSERT INTO dbo.filter_driver_altitudes
VALUES
('Activity Monitor', 'DTPL.sys', 388520.00, 'CONNECT SHIFT LTD');
INSERT INTO dbo.filter_driver_altitudes
VALUES
('Activity Monitor', 'CyOptics.sys', 388514.00, 'Cylance Inc.');
INSERT INTO dbo.filter_driver_altitudes
VALUES
('Activity Monitor', 'CyProtectDrv32.sys', 388510.00, 'Cylance Inc.');
INSERT INTO dbo.filter_driver_altitudes
VALUES
('Activity Monitor', 'CyProtectDrv64.sys', 388510.00, 'Cylance Inc.');
INSERT INTO dbo.filter_driver_altitudes
VALUES
('Activity Monitor', 'tbfsfilt.sys', 388500.00, 'Third Brigade');
INSERT INTO dbo.filter_driver_altitudes
VALUES
('Activity Monitor', 'IvAppMon.sys', 388491.00, 'Ivanti');
INSERT INTO dbo.filter_driver_altitudes
VALUES
('Activity Monitor', 'LDSecDrv.sys', 388490.00, 'LANDESK Software');
INSERT INTO dbo.filter_driver_altitudes
VALUES
('Activity Monitor', 'SGResFlt.sys', 388480.00, 'Samsung SDS Ltd');
INSERT INTO dbo.filter_driver_altitudes
VALUES
('Activity Monitor', 'CwMem2k64.sys', 388470.00, 'ApSoft');
INSERT INTO dbo.filter_driver_altitudes
VALUES
('Activity Monitor', 'axfltdrv.sys', 388460.00, 'Axact Pvt Ltd');
INSERT INTO dbo.filter_driver_altitudes
VALUES
('Activity Monitor', 'RMDiskMon.sys', 388450.00, 'Qingdao Ruanmei Network Technology Co., Ltd.');
INSERT INTO dbo.filter_driver_altitudes
VALUES
('Activity Monitor', 'diskactmon.sys', 388440.00, 'Qingdao Ruanmei Network Technology Co., Ltd.');
INSERT INTO dbo.filter_driver_altitudes
VALUES
('Activity Monitor', 'BlackCat.sys', 388435.00, 'NEXON KOREA');
INSERT INTO dbo.filter_driver_altitudes
VALUES
('Activity Monitor', 'Codex.sys', 388430.00, 'GameHi Co., Ltd.');
INSERT INTO dbo.filter_driver_altitudes
VALUES
('Activity Monitor', 'CatMF.sys', 388420.00, 'Logichron Inc');
INSERT INTO dbo.filter_driver_altitudes
VALUES
('Activity Monitor', 'RW7FsFlt.sys', 388410.00, 'PJSC KP VTI');
INSERT INTO dbo.filter_driver_altitudes
VALUES
('Activity Monitor', 'aswSP.sys', 388401.00, 'Avast Software');
INSERT INTO dbo.filter_driver_altitudes
VALUES
('Activity Monitor', 'aswFsBlk.sys', 388400.00, 'ALWIL Software');
INSERT INTO dbo.filter_driver_altitudes
VALUES
('Activity Monitor', 'AbrPmon.sys', 388390.00, 'FastTrack Software ApS');
INSERT INTO dbo.filter_driver_altitudes
VALUES
('Activity Monitor', 'ThreatStackFIM.sys', 388380.00, 'Threat Stack');
INSERT INTO dbo.filter_driver_altitudes
VALUES
('Activity Monitor', 'BOsCmFlt.sys', 388370.00, 'Barkly Protects Inc.');
INSERT INTO dbo.filter_driver_altitudes
VALUES
('Activity Monitor', 'BOsFsFltr.sys', 388370.00, 'Barkly Protects Inc.');
INSERT INTO dbo.filter_driver_altitudes
VALUES
('Activity Monitor', 'Asgard.sys', 388365.00, 'SPEKNET EOOD');
INSERT INTO dbo.filter_driver_altitudes
VALUES
('Activity Monitor', 'FeKern.sys', 388360.00, 'FireEye Inc.');
INSERT INTO dbo.filter_driver_altitudes
VALUES
('Activity Monitor', 'fhfs.sys', 388355.50, 'SecureCircle');
INSERT INTO dbo.filter_driver_altitudes
VALUES
('Activity Monitor', 'libwaacd.sys', 388350.20, 'OPSWAT Inc.');
INSERT INTO dbo.filter_driver_altitudes
VALUES
('Activity Monitor', 'libwamf.sys', 388350.00, 'OPSWAT Inc.');
INSERT INTO dbo.filter_driver_altitudes
VALUES
('Activity Monitor', 'SZEDRDrv.sys', 388346.00, 'SaferZone Co.');
INSERT INTO dbo.filter_driver_altitudes
VALUES
('Activity Monitor', 'szardrv.sys', 388345.00, 'SaferZone Co.');
INSERT INTO dbo.filter_driver_altitudes
VALUES
('Activity Monitor', 'szpcmdrv.sys', 388341.00, 'SaferZone Co.');
INSERT INTO dbo.filter_driver_altitudes
VALUES
('Activity Monitor', 'szdfmdrv.sys', 388340.00, 'SaferZone Co.');
INSERT INTO dbo.filter_driver_altitudes
VALUES
('Activity Monitor', 'szdfmdrv_usb.sys', 388331.00, 'SaferZone Co.');
INSERT INTO dbo.filter_driver_altitudes
VALUES
('Activity Monitor', 'sprtdrv.sys', 388330.00, 'SaferZone Co.');
INSERT INTO dbo.filter_driver_altitudes
VALUES
('Activity Monitor', 'SWFsFltrv2.sys', 388321.00, 'Solarwinds LLC');
INSERT INTO dbo.filter_driver_altitudes
VALUES
('Activity Monitor', 'SWFsFltr.sys', 388320.00, 'Solarwinds LLC');
INSERT INTO dbo.filter_driver_altitudes
VALUES
('Activity Monitor', 'flashaccelfs.sys', 388310.00, 'Network Appliance');
INSERT INTO dbo.filter_driver_altitudes
VALUES
('Activity Monitor', 'changelog.sys', 388300.00, 'Network Appliance');
INSERT INTO dbo.filter_driver_altitudes
VALUES
('Activity Monitor', 'stcvsm.sys', 388250.00, 'StorageCraft Tech');
INSERT INTO dbo.filter_driver_altitudes
VALUES
('Activity Monitor', 'aUpDrv.sys', 388240.00, 'ITSTATION Inc');
INSERT INTO dbo.filter_driver_altitudes
VALUES
('Activity Monitor', 'fshs.sys', 388222.00, 'F-Secure');
INSERT INTO dbo.filter_driver_altitudes
VALUES
('Activity Monitor', 'fshs.sys', 388221.00, 'F-Secure');
INSERT INTO dbo.filter_driver_altitudes
VALUES
('Activity Monitor', 'fsatp.sys', 388220.00, 'F-Secure');
INSERT INTO dbo.filter_driver_altitudes
VALUES
('Activity Monitor', 'SecdoDriver.sys', 388210.00, 'Secdo');
INSERT INTO dbo.filter_driver_altitudes
VALUES
('Activity Monitor', 'TGFSMF.sys', 388200.00, 'Tetraglyph Technologies');
INSERT INTO dbo.filter_driver_altitudes
VALUES
('Activity Monitor', 'napfflti.sys', 388150.50, 'NETAND Co Ltd');
INSERT INTO dbo.filter_driver_altitudes
VALUES
('Activity Monitor', 'OwlyshieldRansomFilter.sys', 388110.50, 'SitinCloud SAS');
INSERT INTO dbo.filter_driver_altitudes
VALUES
('Activity Monitor', 'evscase.sys', 388100.00, 'March Hare Software Ltd');
INSERT INTO dbo.filter_driver_altitudes
VALUES
('Activity Monitor', 'VSScanner.sys', 388050.00, 'VoodooSoft, LLC');
INSERT INTO dbo.filter_driver_altitudes
VALUES
('Activity Monitor', 'HDRansomOffDrv.sys', 388044.00, 'Heilig Defense LLC');
INSERT INTO dbo.filter_driver_altitudes
VALUES
('Activity Monitor', 'HDCorrelateFDrv.sys', 388042.00, 'Heilig Defense LLC');
INSERT INTO dbo.filter_driver_altitudes
VALUES
('Activity Monitor', 'HDFileMon.sys', 388040.00, 'Heilig Defense LLC');
INSERT INTO dbo.filter_driver_altitudes
VALUES
('Activity Monitor', 'tsifilemon.sys', 388012.00, 'Intercom Inc.');
INSERT INTO dbo.filter_driver_altitudes
VALUES
('Activity Monitor', 'MarSpy.sys', 388010.00, 'Intercom Inc.');
INSERT INTO dbo.filter_driver_altitudes
VALUES
('Activity Monitor', 'AGSysLock.sys', 388002.00, 'AppGuard LLC');
INSERT INTO dbo.filter_driver_altitudes
VALUES
('Activity Monitor', 'AGSecLock.sys', 388001.00, 'AppGuard LLC');
INSERT INTO dbo.filter_driver_altitudes
VALUES
('Activity Monitor', 'BrnFileLock.sys', 388000.00, 'Blue Ridge Networks');
INSERT INTO dbo.filter_driver_altitudes
VALUES
('Activity Monitor', 'BrnSecLock.sys', 387990.00, 'Blue Ridge Networks');
INSERT INTO dbo.filter_driver_altitudes
VALUES
('Activity Monitor', 'LCmPrintMon.sys', 387978.00, 'YATEM Co. Ltd.');
INSERT INTO dbo.filter_driver_altitudes
VALUES
('Activity Monitor', 'LCgAdMon.sys', 387977.00, 'YATEM Co. Ltd.');
INSERT INTO dbo.filter_driver_altitudes
VALUES
('Activity Monitor', 'LCmAdMon.sys', 387976.00, 'YATEM Co. Ltd.');
INSERT INTO dbo.filter_driver_altitudes
VALUES
('Activity Monitor', 'LCgFileMon.sys', 387975.00, 'YATEM Co. Ltd.');
INSERT INTO dbo.filter_driver_altitudes
VALUES
('Activity Monitor', 'LCmFile.sys', 387974.00, 'YATEM Co. Ltd.');
INSERT INTO dbo.filter_driver_altitudes
VALUES
('Activity Monitor', 'LCgFile.sys', 387972.00, 'YATEM Co. Ltd.');
INSERT INTO dbo.filter_driver_altitudes
VALUES
('Activity Monitor', 'LCmFileMon.sys', 387970.00, 'YATEM Co. Ltd.');
INSERT INTO dbo.filter_driver_altitudes
VALUES
('Activity Monitor', 'IridiumSwitch.sys', 387960.00, 'Confio');
INSERT INTO dbo.filter_driver_altitudes
VALUES
('Activity Monitor', 'axfsysmon.sys', 387951.00, 'AppiXoft');
INSERT INTO dbo.filter_driver_altitudes
VALUES
('Activity Monitor', 'scensemon.sys', 387950.00, 'AppiXoft');
INSERT INTO dbo.filter_driver_altitudes
VALUES
('Activity Monitor', 'ruaff.sys', 387940.00, 'RUNEXY');
INSERT INTO dbo.filter_driver_altitudes
VALUES
('Activity Monitor', 'bbfilter.sys', 387930.00, 'derivo GmbH');
INSERT INTO dbo.filter_driver_altitudes
VALUES
('Activity Monitor', 'Bfmon.sys', 387920.00, 'Baidu (Hong Kong) Limited');
INSERT INTO dbo.filter_driver_altitudes
VALUES
('Activity Monitor', 'bdsysmon.sys', 387912.00, 'Baidu Online Network');
INSERT INTO dbo.filter_driver_altitudes
VALUES
('Activity Monitor', 'BdRdFolder.sys', 387910.00, 'Baidu (beijing)');
INSERT INTO dbo.filter_driver_altitudes
VALUES
('Activity Monitor', 'mlsaff.sys', 387901.00, 'RUNEXY');
INSERT INTO dbo.filter_driver_altitudes
VALUES
('Activity Monitor', 'pscff.sys', 387900.00, 'Weing Co.,Ltd.');
INSERT INTO dbo.filter_driver_altitudes
VALUES
('Activity Monitor', 'fcnotify.sys', 387880.00, 'TCXA Ltd.');
INSERT INTO dbo.filter_driver_altitudes
VALUES
('Activity Monitor', 'aaf.sys', 387860.00, 'Actifio Inc');
INSERT INTO dbo.filter_driver_altitudes
VALUES
('Activity Monitor', 'gddcv.sys', 387840.00, 'G Data Software AG');
INSERT INTO dbo.filter_driver_altitudes
VALUES
('Activity Monitor', 'wgfile.sys', 387820.00, 'ORANGE WERKS Inc');
INSERT INTO dbo.filter_driver_altitudes
VALUES
('Activity Monitor', 'zesfsmf.sys', 387800.00, 'Novell');
INSERT INTO dbo.filter_driver_altitudes
VALUES
('Activity Monitor', 'BWAnticheat.sys', 387750.00, 'Binklac Workstation');
INSERT INTO dbo.filter_driver_altitudes
VALUES
('Activity Monitor', 'uamflt.sys', 387700.00, 'Sevtechnotrans');
INSERT INTO dbo.filter_driver_altitudes
VALUES
('Activity Monitor', 'ehdrv.sys', 387600.00, 'ESET, spol. s r.o.');
INSERT INTO dbo.filter_driver_altitudes
VALUES
('Activity Monitor', 'DattoFSF.sys', 387560.00, 'Datto Inc');
INSERT INTO dbo.filter_driver_altitudes
VALUES
('Activity Monitor', 'RubrikFileAudit.sys', 387552.00, 'Rubrik Inc');
INSERT INTO dbo.filter_driver_altitudes
VALUES
('Activity Monitor', 'FileSystemCBT.sys', 387550.00, 'Rubrik Inc');
INSERT INTO dbo.filter_driver_altitudes
VALUES
('Activity Monitor', 'Snilog.sys', 387500.00, 'Systemneeds, Inc');
INSERT INTO dbo.filter_driver_altitudes
VALUES
('Activity Monitor', 'tss.sys', 387400.00, 'Tiversa Inc');
INSERT INTO dbo.filter_driver_altitudes
VALUES
('Activity Monitor', 'LmDriver.sys', 387390.00, 'in-soft Kft.');
INSERT INTO dbo.filter_driver_altitudes
VALUES
('Activity Monitor', 'WDCFilter.sys', 387330.00, 'Interset Inc.');
INSERT INTO dbo.filter_driver_altitudes
VALUES
('Activity Monitor', 'altcbt.sys', 387320.00, 'Altaro Ltd.');
INSERT INTO dbo.filter_driver_altitudes
VALUES
('Activity Monitor', 'bapfecpt.sys', 387310.00, 'BitArmor Systems, Inc');
INSERT INTO dbo.filter_driver_altitudes
VALUES
('Activity Monitor', 'bamfltr.sys', 387300.00, 'BitArmor Systems, Inc');
INSERT INTO dbo.filter_driver_altitudes
VALUES
('Activity Monitor', 'TrustedEdgeFfd.sys', 387200.00, 'FileTek, Inc.');
INSERT INTO dbo.filter_driver_altitudes
VALUES
('Activity Monitor', 'MRxGoogle.sys', 387100.00, 'Google, Inc.');
INSERT INTO dbo.filter_driver_altitudes
VALUES
('Activity Monitor', 'YFSDR.SYS', 387010.00, 'Yokogawa R&L Corp');
INSERT INTO dbo.filter_driver_altitudes
VALUES
('Activity Monitor', 'YFSD.SYS', 387000.00, 'Yokogawa R&L Corp');
INSERT INTO dbo.filter_driver_altitudes
VALUES
('Activity Monitor', 'YFSRD.sys', 386990.00, 'Yokogawa R&L Corp');
INSERT INTO dbo.filter_driver_altitudes
VALUES
('Activity Monitor', 'psgfoctrl.sys', 386990.00, 'Yokogawa R&L Corp');
INSERT INTO dbo.filter_driver_altitudes
VALUES
('Activity Monitor', 'psgdflt.sys', 386980.00, 'Yokogawa R&L Corp');
INSERT INTO dbo.filter_driver_altitudes
VALUES
('Activity Monitor', 'USBPDH.SYS', 386901.00, 'Centre for Development of Advanced Computing');
INSERT INTO dbo.filter_driver_altitudes
VALUES
('Activity Monitor', 'pecfilter.sys', 386900.00, 'C-DAC Hyderabad');
INSERT INTO dbo.filter_driver_altitudes
VALUES
('Activity Monitor', 'GKPFCB64.sys', 386810.00, 'INCA Internet Co.,Ltd.');
INSERT INTO dbo.filter_driver_altitudes
VALUES
('Activity Monitor', 'TkPcFtCb.sys on 32bit', 386800.00, 'INCA Internet Co.,Ltd.');
INSERT INTO dbo.filter_driver_altitudes
VALUES
('Activity Monitor', 'TkPcFtCb64.sys on 64bit', 386800.00, 'INCA Internet Co.,Ltd.');
INSERT INTO dbo.filter_driver_altitudes
VALUES
('Activity Monitor', 'bmregdrv.sys', 386731.00, 'Yandex LLC');
INSERT INTO dbo.filter_driver_altitudes
VALUES
('Activity Monitor', 'bmfsdrv.sys', 386730.00, 'Yandex LLC');
INSERT INTO dbo.filter_driver_altitudes
VALUES
('Activity Monitor', 'CarbonBlackK.sys', 386720.00, 'Bit9 Inc.');
INSERT INTO dbo.filter_driver_altitudes
VALUES
('Activity Monitor', 'ScAuthFSFlt2.sys', 386711.00, 'Security Code LLC');
INSERT INTO dbo.filter_driver_altitudes
VALUES
('Activity Monitor', 'ScAuthFSFlt.sys', 386710.00, 'Security Code LLC');
INSERT INTO dbo.filter_driver_altitudes
VALUES
('Activity Monitor', 'ScAuthIoDrv.sys', 386700.00, 'Security Code LLC');
INSERT INTO dbo.filter_driver_altitudes
VALUES
('Activity Monitor', 'mfeaskm.sys', 386610.00, 'McAfee Inc.');
INSERT INTO dbo.filter_driver_altitudes
VALUES
('Activity Monitor', 'mfencfilter.sys', 386600.00, 'McAfee');
INSERT INTO dbo.filter_driver_altitudes
VALUES
('Activity Monitor', 'WinFLAHdrv.sys', 386540.00, 'NewSoftwares.net,Inc.');
INSERT INTO dbo.filter_driver_altitudes
VALUES
('Activity Monitor', 'WinFLAdrv.sys', 386530.00, 'NewSoftwares.net,Inc.');
INSERT INTO dbo.filter_driver_altitudes
VALUES
('Activity Monitor', 'WinDBdrv.sys', 386520.00, 'NewSoftwares.net,Inc.');
INSERT INTO dbo.filter_driver_altitudes
VALUES
('Activity Monitor', 'WinFLdrv.sys', 386510.00, 'NewSoftwares.net,Inc.');
INSERT INTO dbo.filter_driver_altitudes
VALUES
('Activity Monitor', 'WinFPdrv.sys', 386500.00, 'NewSoftwares.net,Inc.');
COMMIT TRAN;
GO
BEGIN TRAN;
INSERT INTO dbo.filter_driver_altitudes
VALUES
('Activity Monitor', 'varpffmon.sys', 386486.00, 'Varlook Ltd.');
INSERT INTO dbo.filter_driver_altitudes
VALUES
('Activity Monitor', 'SkyWPDrv.sys', 386435.00, 'Sky Co.,Ltd.');
INSERT INTO dbo.filter_driver_altitudes
VALUES
('Activity Monitor', 'SkyRGDrv.sys', 386431.00, 'Sky Co., LTD.');
INSERT INTO dbo.filter_driver_altitudes
VALUES
('Activity Monitor', 'SkyAMDrv.sys', 386430.00, 'Sky Co., LTD.');
INSERT INTO dbo.filter_driver_altitudes
VALUES
('Activity Monitor', 'SheedSelfProtection.sys', 386421.00, 'SheedSoft Ltd');
INSERT INTO dbo.filter_driver_altitudes
VALUES
('Activity Monitor', 'arta.sys', 386420.00, 'SheedSoft Ltd.');
INSERT INTO dbo.filter_driver_altitudes
VALUES
('Activity Monitor', 'ApexSqlFilterDriver.sys', 386410.00, 'ApexSQL LLC');
INSERT INTO dbo.filter_driver_altitudes
VALUES
('Activity Monitor', 'stflt.sys', 386400.00, 'Xacti');
INSERT INTO dbo.filter_driver_altitudes
VALUES
('Activity Monitor', 'tbrdrv.sys', 386390.00, 'Crawler Group');
INSERT INTO dbo.filter_driver_altitudes
VALUES
('Activity Monitor', 'WinTeonMiniFilter.sys', 386320.00, 'Dmitry Stefankov');
INSERT INTO dbo.filter_driver_altitudes
VALUES
('Activity Monitor', 'wiper.sys', 386310.00, 'Dmitry Stefankov');
INSERT INTO dbo.filter_driver_altitudes
VALUES
('Activity Monitor', 'DevMonMiniFilter.sys', 386300.00, 'Dmitry Stefankov');
INSERT INTO dbo.filter_driver_altitudes
VALUES
('Activity Monitor', 'VMWVvpfsd.sys', 386200.00, 'VMware, Inc.');
INSERT INTO dbo.filter_driver_altitudes
VALUES
('Activity Monitor', 'RTOLogon.sys (Renamed)', 386200.00, 'VMware, Inc.');
INSERT INTO dbo.filter_driver_altitudes
VALUES
('Activity Monitor', 'Code42Filter.sys', 386190.00, 'Code42');
INSERT INTO dbo.filter_driver_altitudes
VALUES
('Activity Monitor', 'AATFilter.sys', 386189.50, 'Code42');
INSERT INTO dbo.filter_driver_altitudes
VALUES
('Activity Monitor', 'ConduantFSFltr.sys', 386180.00, 'Conduant Corporation');
INSERT INTO dbo.filter_driver_altitudes
VALUES
('Activity Monitor', 'KtFSFilter.sys', 386170.00, 'Keysight Technologies');
INSERT INTO dbo.filter_driver_altitudes
VALUES
('Activity Monitor', 'FileGuard.sys', 386140.00, 'RES Software');
INSERT INTO dbo.filter_driver_altitudes
VALUES
('Activity Monitor', 'NetGuard.sys', 386130.00, 'RES Software');
INSERT INTO dbo.filter_driver_altitudes
VALUES
('Activity Monitor', 'RegGuard.sys', 386120.00, 'RES Software');
INSERT INTO dbo.filter_driver_altitudes
VALUES
('Activity Monitor', 'ImgGuard.sys', 386110.00, 'RES Software');
INSERT INTO dbo.filter_driver_altitudes
VALUES
('Activity Monitor', 'AppGuard.sys', 386100.00, 'RES Software');
INSERT INTO dbo.filter_driver_altitudes
VALUES
('Activity Monitor', 'RuiDiskFs.sys', 386030.00, 'RuiGuard Ltd');
INSERT INTO dbo.filter_driver_altitudes
VALUES
('Activity Monitor', 'minitrc.sys', 386020.00, 'Protected Networks');
INSERT INTO dbo.filter_driver_altitudes
VALUES
('Activity Monitor', 'cpepmon.sys', 386010.00, 'Checkpoint Software');
INSERT INTO dbo.filter_driver_altitudes
VALUES
('Activity Monitor', 'CGWMF.sys', 386000.00, 'NetIQ');
INSERT INTO dbo.filter_driver_altitudes
VALUES
('Activity Monitor', 'ISRegFlt.sys', 385990.00, 'Flexera Software Inc.');
INSERT INTO dbo.filter_driver_altitudes
VALUES
('Activity Monitor', 'ISRegFlt64.sys', 385990.00, 'Flexera Software Inc.');
INSERT INTO dbo.filter_driver_altitudes
VALUES
('Activity Monitor', 'shdlpSf.sys', 385970.00, 'Comtrue Technology');
INSERT INTO dbo.filter_driver_altitudes
VALUES
('Activity Monitor', 'ctrPAMon.sys', 385960.00, 'Comtrue Technology');
INSERT INTO dbo.filter_driver_altitudes
VALUES
('Activity Monitor', 'shdlpMedia.sys', 385950.00, 'Comtrue Technology');
INSERT INTO dbo.filter_driver_altitudes
VALUES
('Activity Monitor', 'SealProtect.sys', 385920.70, 'Beijing Bytedance');
INSERT INTO dbo.filter_driver_altitudes
VALUES
('Activity Monitor', 'FLProtect.sys', 385920.50, 'Beijing Volcano Engine');
INSERT INTO dbo.filter_driver_altitudes
VALUES
('Activity Monitor', 'immflex.sys', 385910.00, 'Immidio B.V.');
INSERT INTO dbo.filter_driver_altitudes
VALUES
('Activity Monitor', 'StegoProtect.sys', 385900.00, 'Stegosystems Inc');
INSERT INTO dbo.filter_driver_altitudes
VALUES
('Activity Monitor', 'brfilter.sys', 385890.00, 'Bromium Inc');
INSERT INTO dbo.filter_driver_altitudes
VALUES
('Activity Monitor', 'BrCow_x_x_x_x.sys', 385889.00, 'Bromium Inc');
INSERT INTO dbo.filter_driver_altitudes
VALUES
('Activity Monitor', 'BemK.sys', 385888.00, 'Bromium Inc');
INSERT INTO dbo.filter_driver_altitudes
VALUES
('Activity Monitor', 'secRMM.sys', 385880.00, 'Squadra Technologies, LLC.');
INSERT INTO dbo.filter_driver_altitudes
VALUES
('Activity Monitor', 'dgfilter.sys', 385870.00, 'DataGravity Inc.');
INSERT INTO dbo.filter_driver_altitudes
VALUES
('Activity Monitor', 'WFP_MRT.sys', 385860.00, 'FireEye Inc');
INSERT INTO dbo.filter_driver_altitudes
VALUES
('Activity Monitor', 'klrsps.sys', 385815.00, 'Kaspersky Lab');
INSERT INTO dbo.filter_driver_altitudes
VALUES
('Activity Monitor', 'klsnsr.sys', 385810.00, 'Kaspersky Lab');
INSERT INTO dbo.filter_driver_altitudes
VALUES
('Activity Monitor', 'TaniumRecorderDrv.sys', 385800.00, 'Tanium');
INSERT INTO dbo.filter_driver_altitudes
VALUES
('Activity Monitor', 'bdsmonsys.sys', 385750.00, 'Binary Defense Systems');
INSERT INTO dbo.filter_driver_altitudes
VALUES
('Activity Monitor', 'CdsgFsFilter.sys', 385700.00, 'CRU Data Security Group');
INSERT INTO dbo.filter_driver_altitudes
VALUES
('Activity Monitor', 'mssecflt.sys', 385600.00, 'Microsoft');
INSERT INTO dbo.filter_driver_altitudes
VALUES
('Activity Monitor', 'Backupreader.sys', 385500.00, 'Microsoft');
INSERT INTO dbo.filter_driver_altitudes
VALUES
('Activity Monitor', 'MsixPackagingToolMonitor.sys', 385410.00, 'Microsoft');
INSERT INTO dbo.filter_driver_altitudes
VALUES
('Activity Monitor', 'AppVMon.sys', 385400.00, 'Microsoft');
INSERT INTO dbo.filter_driver_altitudes
VALUES
('Activity Monitor', 'DpmFilter.sys', 385300.00, 'Microsoft');
INSERT INTO dbo.filter_driver_altitudes
VALUES
('Activity Monitor', 'Procmon11.sys', 385200.00, 'Microsoft');
INSERT INTO dbo.filter_driver_altitudes
VALUES
('Activity Monitor', 'wtd.sys', 385110.00, 'Microsoft');
INSERT INTO dbo.filter_driver_altitudes
VALUES
('Activity Monitor', 'minispy.sys - Top', 385100.00, 'Microsoft');
INSERT INTO dbo.filter_driver_altitudes
VALUES
('Activity Monitor', 'fdrtrace.sys', 385001.00, 'Microsoft');
INSERT INTO dbo.filter_driver_altitudes
VALUES
('Activity Monitor', 'filetrace.sys', 385000.00, 'Microsoft');
INSERT INTO dbo.filter_driver_altitudes
VALUES
('Activity Monitor', 'uwfreg.sys', 384910.00, 'Microsoft');
INSERT INTO dbo.filter_driver_altitudes
VALUES
('Activity Monitor', 'uwfs.sys', 384900.00, 'Microsoft');
INSERT INTO dbo.filter_driver_altitudes
VALUES
('Activity Monitor', 'locksmith.sys', 384800.00, 'Microsoft');
INSERT INTO dbo.filter_driver_altitudes
VALUES
('Activity Monitor', 'winload.sys', 384700.00, 'Microsoft');
INSERT INTO dbo.filter_driver_altitudes
VALUES
('Activity Monitor', 'SFPMonitor.sys - Top', 383350.00, 'SonicWall Inc');
INSERT INTO dbo.filter_driver_altitudes
VALUES
('Activity Monitor', 'FilrDriver.sys', 383340.00, 'Micro Focus');
INSERT INTO dbo.filter_driver_altitudes
VALUES
('Activity Monitor', 'rwchangedrv.sys', 383330.00, 'Rackware');
INSERT INTO dbo.filter_driver_altitudes
VALUES
('Activity Monitor', 'airship-filter.sys', 383320.00, 'AIRWare Technology Ltd');
INSERT INTO dbo.filter_driver_altitudes
VALUES
('Activity Monitor', 'AeFilter.sys', 383310.00, 'Faronics Corporation');
INSERT INTO dbo.filter_driver_altitudes
VALUES
('Activity Monitor', 'QQProtect.sys', 383300.00, 'Tencent (Shenzhen)');
INSERT INTO dbo.filter_driver_altitudes
VALUES
('Activity Monitor', 'QQProtectX64.sys', 383300.00, 'Tencent (Shenzhen)');
INSERT INTO dbo.filter_driver_altitudes
VALUES
('Activity Monitor', 'KernelAgent32.sys', 383260.00, 'ZoneFox');
INSERT INTO dbo.filter_driver_altitudes
VALUES
('Activity Monitor', 'WRDWIZFILEPROT.SYS', 383251.00, 'WardWiz');
INSERT INTO dbo.filter_driver_altitudes
VALUES
('Activity Monitor', 'WRDWIZREGPROT.SYS', 383250.00, 'WardWiz');
INSERT INTO dbo.filter_driver_altitudes
VALUES
('Activity Monitor', 'groundling32.sys', 383200.00, 'Dell Secureworks');
INSERT INTO dbo.filter_driver_altitudes
VALUES
('Activity Monitor', 'groundling64.sys', 383200.00, 'Dell Secureworks');
INSERT INTO dbo.filter_driver_altitudes
VALUES
('Activity Monitor', 'avgtpx86.sys', 383190.00, 'AVG Technologies CZ, s.r.o');
INSERT INTO dbo.filter_driver_altitudes
VALUES
('Activity Monitor', 'avgtpx64.sys', 383190.00, 'AVG Technologies CZ, s.r.o');
INSERT INTO dbo.filter_driver_altitudes
VALUES
('Activity Monitor', 'DataNow_Driver.sys', 383182.00, 'AppSense Ltd');
INSERT INTO dbo.filter_driver_altitudes
VALUES
('Activity Monitor', 'UcaFltDriver.sys', 383180.00, 'AppSense Ltd');
INSERT INTO dbo.filter_driver_altitudes
VALUES
('Activity Monitor', 'YFSD2.sys', 383170.00, 'Yokogawa Corpration');
INSERT INTO dbo.filter_driver_altitudes
VALUES
('Activity Monitor', 'Kisknl.sys', 383160.00, 'kingsoft');
INSERT INTO dbo.filter_driver_altitudes
VALUES
('Activity Monitor', 'MWatcher.sys', 383150.00, 'Neowiz Corporation');
INSERT INTO dbo.filter_driver_altitudes
VALUES
('Activity Monitor', 'tsifilemon.sys', 383140.00, 'Intercom Inc.');
INSERT INTO dbo.filter_driver_altitudes
VALUES
('Activity Monitor', 'FIM.sys', 383130.00, 'eIQnetworks Inc.');
INSERT INTO dbo.filter_driver_altitudes
VALUES
('Activity Monitor', 'cFSfdrv', 383120.00, 'Chaewool');
INSERT INTO dbo.filter_driver_altitudes
VALUES
('Activity Monitor', 'ajfsprot.sys', 383110.00, 'Analytik Jena AG');
INSERT INTO dbo.filter_driver_altitudes
VALUES
('Activity Monitor', 'isafermon', 383100.00, '(c)SMS');
INSERT INTO dbo.filter_driver_altitudes
VALUES
('Activity Monitor', 'kfac.sys', 383000.00, 'Beijing CA-JinChen Software Co., Ltd');
INSERT INTO dbo.filter_driver_altitudes
VALUES
('Activity Monitor', 'GUMHFilter.sys', 382910.00, 'Glarysoft Ltd.');
INSERT INTO dbo.filter_driver_altitudes
VALUES
('Activity Monitor', 'PsAcFileAccessFilter.sys', 382902.00, 'FUJITSU SOFTWARE');
INSERT INTO dbo.filter_driver_altitudes
VALUES
('Activity Monitor', 'FJGSDis2.sys', 382900.00, 'FUJITSU LIMITED');
INSERT INTO dbo.filter_driver_altitudes
VALUES
('Activity Monitor', 'secure_os.sys', 382890.00, 'FUJITSU SOCIAL SCIENCE');
INSERT INTO dbo.filter_driver_altitudes
VALUES
('Activity Monitor', 'ibr2fsk.sys', 382880.00, 'FUJITSU ENGINEERING');
INSERT INTO dbo.filter_driver_altitudes
VALUES
('Activity Monitor', 'FJSeparettiFilterRedirect.sys', 382860.00, 'FUJITSU LIMITED');
INSERT INTO dbo.filter_driver_altitudes
VALUES
('Activity Monitor', 'Fsw31rj1.sys', 382855.00, 'FUJITSU LIMITED');
INSERT INTO dbo.filter_driver_altitudes
VALUES
('Activity Monitor', 'da_ctl.sys', 382850.00, 'FUJITSU LIMITED');
INSERT INTO dbo.filter_driver_altitudes
VALUES
('Activity Monitor', 'zqFilter.sys', 382800.00, 'magrasoft Ltd');
INSERT INTO dbo.filter_driver_altitudes
VALUES
('Activity Monitor', 'ntps_fa.sys', 382700.00, 'DefendX Software');
INSERT INTO dbo.filter_driver_altitudes
VALUES
('Activity Monitor', 'ancfunc.sys', 382650.00, 'Aunaki');
INSERT INTO dbo.filter_driver_altitudes
VALUES
('Activity Monitor', 'sConnect.sys', 382600.00, 'I-O DATA DEVICE, INC>');
INSERT INTO dbo.filter_driver_altitudes
VALUES
('Activity Monitor', 'AdaptivaClientCache32.sys', 382500.00, 'Adaptiva');
INSERT INTO dbo.filter_driver_altitudes
VALUES
('Activity Monitor', 'AdaptivaclientCache64.sys', 382500.00, 'Adaptiva');
INSERT INTO dbo.filter_driver_altitudes
VALUES
('Activity Monitor', 'phantomd.sys', 382440.00, 'Veramine Inc');
INSERT INTO dbo.filter_driver_altitudes
VALUES
('Activity Monitor', 'GoFSMF.sys', 382430.00, 'Gorizonty Rosta Ltd');
INSERT INTO dbo.filter_driver_altitudes
VALUES
('Activity Monitor', 'SWCommFltr.sys', 382420.00, 'SnoopWall LLC');
INSERT INTO dbo.filter_driver_altitudes
VALUES
('Activity Monitor', 'atflt.sys', 382410.00, 'Atlansys Software, LLC');
INSERT INTO dbo.filter_driver_altitudes
VALUES
('Activity Monitor', 'amfd.sys', 382400.00, 'Atlansys Software, LLC');
INSERT INTO dbo.filter_driver_altitudes
VALUES
('Activity Monitor', 'iSafeKrnl.sys', 382390.00, 'Elex Tech Inc');
INSERT INTO dbo.filter_driver_altitudes
VALUES
('Activity Monitor', 'iSafeKrnlMon.sys', 382380.00, 'Elex Tech Inc');
INSERT INTO dbo.filter_driver_altitudes
VALUES
('Activity Monitor', 'AtdrAgent.sys', 382325.00, '360 Software (Beijing)');
INSERT INTO dbo.filter_driver_altitudes
VALUES
('Activity Monitor', 'AtdrAgent64.sys', 382325.00, '360 Software (Beijing)');
INSERT INTO dbo.filter_driver_altitudes
VALUES
('Activity Monitor', 'Qutmdrv.sys', 382320.00, '360 Software (Beijing)');
INSERT INTO dbo.filter_driver_altitudes
VALUES
('Activity Monitor', '360box.sys', 382310.00, 'Qihoo 360');
INSERT INTO dbo.filter_driver_altitudes
VALUES
('Activity Monitor', '360fsflt.sys', 382300.00, 'Beijing Qihoo Technology Co., Ltd.');
INSERT INTO dbo.filter_driver_altitudes
VALUES
('Activity Monitor', 'PSFSF.sys', 382250.50, 'Peer Software Inc');
INSERT INTO dbo.filter_driver_altitudes
VALUES
('Activity Monitor', 'scred.sys', 382210.00, 'SoftCamp Co., Ltd.');
INSERT INTO dbo.filter_driver_altitudes
VALUES
('Activity Monitor', 'PDGenFam.sys', 382200.00, 'Soluto LTD');
INSERT INTO dbo.filter_driver_altitudes
VALUES
('Activity Monitor', 'MCFileMon64.sys (x64 systems)', 382100.00, 'Sumitomo Electric Ltd.');
INSERT INTO dbo.filter_driver_altitudes
VALUES
('Activity Monitor', 'MCFileMon32.sys (x32 systems)', 382100.00, 'Sumitomo Electric Ltd.');
INSERT INTO dbo.filter_driver_altitudes
VALUES
('Activity Monitor', 'RyGuard.sys', 382050.00, 'SHENZHEN UNNOO Information Techco., Ltd');
INSERT INTO dbo.filter_driver_altitudes
VALUES
('Activity Monitor', 'FileShareMon.sys', 382040.00, 'SHENZHEN UNNOO Information Techco., Ltd');
INSERT INTO dbo.filter_driver_altitudes
VALUES
('Activity Monitor', 'ryfilter.sys', 382030.00, 'SHENZHEN UNNOO Information Techco., Ltd');
COMMIT TRAN;
GO
BEGIN TRAN;
INSERT INTO dbo.filter_driver_altitudes
VALUES
('Activity Monitor', 'secufile.sys', 382020.00, 'Shenzhen Unnoo LTD');
INSERT INTO dbo.filter_driver_altitudes
VALUES
('Activity Monitor', 'XiaobaiFs.sys', 382010.00, 'Shenzhen Unnoo LTD');
INSERT INTO dbo.filter_driver_altitudes
VALUES
('Activity Monitor', 'XiaobaiFsR.sys', 382000.00, 'Shenzhen Unnoo LTD');
INSERT INTO dbo.filter_driver_altitudes
VALUES
('Activity Monitor', 'TWBDCFilter.sys', 381910.00, 'Trustwave');
INSERT INTO dbo.filter_driver_altitudes
VALUES
('Activity Monitor', 'VPDrvNt.sys', 381900.00, 'AhnLab, Inc.');
INSERT INTO dbo.filter_driver_altitudes
VALUES
('Activity Monitor', 'eetd32.sys', 381800.00, 'Entrust Inc.');
INSERT INTO dbo.filter_driver_altitudes
VALUES
('Activity Monitor', 'eetd64.sys', 381800.00, 'Entrust Inc.');
INSERT INTO dbo.filter_driver_altitudes
VALUES
('Activity Monitor', 'dnaFSMonitor.sys', 381700.00, 'Dtex Systems');
INSERT INTO dbo.filter_driver_altitudes
VALUES
('Activity Monitor', 'iwhlp2.sys on 2000', 381610.00, 'InfoWatch');
INSERT INTO dbo.filter_driver_altitudes
VALUES
('Activity Monitor', 'iwhlpxp.sys on XP', 381610.00, 'InfoWatch');
INSERT INTO dbo.filter_driver_altitudes
VALUES
('Activity Monitor', 'iwhlp.sys on Vista', 381610.00, 'InfoWatch');
INSERT INTO dbo.filter_driver_altitudes
VALUES
('Activity Monitor', 'iwdmfs.sys', 381600.00, 'InfoWatch');
INSERT INTO dbo.filter_driver_altitudes
VALUES
('Activity Monitor', 'IronGateFD.sys', 381500.00, 'rubysoft');
INSERT INTO dbo.filter_driver_altitudes
VALUES
('Activity Monitor', 'MagicBackupMonitor.sys', 381400.00, 'Magic Softworks, Inc.');
INSERT INTO dbo.filter_driver_altitudes
VALUES
('Activity Monitor', 'Sonar.sys', 381337.00, 'IKARUS Security');
INSERT INTO dbo.filter_driver_altitudes
VALUES
('Activity Monitor', 'IPFilter.sys', 381310.00, 'Jinfengshuntai');
INSERT INTO dbo.filter_driver_altitudes
VALUES
('Activity Monitor', 'MSpy.sys', 381300.00, 'Ladislav Zezula');
INSERT INTO dbo.filter_driver_altitudes
VALUES
('Activity Monitor', 'inuse.sys', 381200.00, 'March Hare Software Ltd');
INSERT INTO dbo.filter_driver_altitudes
VALUES
('Activity Monitor', 'qfmon.sys', 381190.00, 'Quality Corporation');
INSERT INTO dbo.filter_driver_altitudes
VALUES
('Activity Monitor', 'FMSRVCIO.sys', 381165.00, 'NEC Solution Innovators');
INSERT INTO dbo.filter_driver_altitudes
VALUES
('Activity Monitor', 'flyfs.sys', 381160.00, 'NEC Soft, Ltd.');
INSERT INTO dbo.filter_driver_altitudes
VALUES
('Activity Monitor', 'serfs.sys', 381150.00, 'NEC Soft, Ltd.');
INSERT INTO dbo.filter_driver_altitudes
VALUES
('Activity Monitor', 'hdrfs.sys', 381140.00, 'NEC Soft, Ltd.');
INSERT INTO dbo.filter_driver_altitudes
VALUES
('Activity Monitor', 'UVMCIFSF.sys', 381130.00, 'NEC Corporation');
INSERT INTO dbo.filter_driver_altitudes
VALUES
('Activity Monitor', 'ICFClientFlt.sys', 381120.00, 'NEC System Technologies,Ltd.');
INSERT INTO dbo.filter_driver_altitudes
VALUES
('Activity Monitor', 'IccFileIoAd.sys', 381110.00, 'NEC System Technologies,Ltd.');
INSERT INTO dbo.filter_driver_altitudes
VALUES
('Activity Monitor', 'IccFilterAudit.sys', 381100.00, 'NEC System Technologies');
INSERT INTO dbo.filter_driver_altitudes
VALUES
('Activity Monitor', 'IccFilterSc.sys', 381090.00, 'InfoCage');
INSERT INTO dbo.filter_driver_altitudes
VALUES
('Activity Monitor', 'Sefo.sys - Top', 381010.00, 'Solusseum Inc');
INSERT INTO dbo.filter_driver_altitudes
VALUES
('Activity Monitor', 'mtsvcdf.sys', 381000.00, 'CristaLink');
INSERT INTO dbo.filter_driver_altitudes
VALUES
('Activity Monitor', 'SDDrvLdr.sys', 380970.00, 'Aliaksander Lebiadzevich');
INSERT INTO dbo.filter_driver_altitudes
VALUES
('Activity Monitor', 'fscbtflt.sys', 380930.50, 'Cohesity Inc');
INSERT INTO dbo.filter_driver_altitudes
VALUES
('Activity Monitor', 'SQLsafeFilterDriver.sys', 380901.00, 'Idera Software');
INSERT INTO dbo.filter_driver_altitudes
VALUES
('Activity Monitor', 'IderaFilterDriver.sys', 380900.00, 'Idera');
INSERT INTO dbo.filter_driver_altitudes
VALUES
('Activity Monitor', 'sie-filemon.sys', 380852.50, 'SN Systems Ltd');
INSERT INTO dbo.filter_driver_altitudes
VALUES
('Activity Monitor', 'cbfilter20.sys', 380852.00, 'SN Systems Ltd');
INSERT INTO dbo.filter_driver_altitudes
VALUES
('Activity Monitor', 'cbfsfilter2017.sys', 380850.00, 'SN Systems Ltd');
INSERT INTO dbo.filter_driver_altitudes
VALUES
('Activity Monitor', 'xhunter1.sys', 380800.00, 'Wellbia.com');
INSERT INTO dbo.filter_driver_altitudes
VALUES
('Activity Monitor', 'iGuard.sys', 380720.00, 'i-Guard SAS');
INSERT INTO dbo.filter_driver_altitudes
VALUES
('Activity Monitor', 'cbfltfs4.sys', 380715.00, 'Nomadesk');
INSERT INTO dbo.filter_driver_altitudes
VALUES
('Activity Monitor', 'cbfltfs4.sys', 380710.00, 'Backup Systems Ltd');
INSERT INTO dbo.filter_driver_altitudes
VALUES
('Activity Monitor', 'PkgFilter.sys', 380700.00, 'Scalable Software Inc.');
INSERT INTO dbo.filter_driver_altitudes
VALUES
('Activity Monitor', 'minifswatcher.sys', 380650.00, 'BITCORP S.R.L.');
INSERT INTO dbo.filter_driver_altitudes
VALUES
('Activity Monitor', 'snimg.sys', 380600.00, 'Softnext Technologies');
INSERT INTO dbo.filter_driver_altitudes
VALUES
('Activity Monitor', 'cbfilter20.sys', 380530.00, 'Brainloop AG');
INSERT INTO dbo.filter_driver_altitudes
VALUES
('Activity Monitor', 'SK.sys', 380520.00, 'HEAT Software');
INSERT INTO dbo.filter_driver_altitudes
VALUES
('Activity Monitor', 'cbfsfilter2017.sys', 380515.00, 'Kits Ltd.');
INSERT INTO dbo.filter_driver_altitudes
VALUES
('Activity Monitor', 'mpxmon.sys', 380510.00, 'Positive Technologies');
INSERT INTO dbo.filter_driver_altitudes
VALUES
('Activity Monitor', 'filenamevalidator.sys', 380502.00, 'Infotecs');
INSERT INTO dbo.filter_driver_altitudes
VALUES
('Activity Monitor', 'SWAgent.sys', 380500.50, 'Stairwell Inc');
INSERT INTO dbo.filter_driver_altitudes
VALUES
('Activity Monitor', 'KC3.sys', 380500.00, 'Infotecs');
INSERT INTO dbo.filter_driver_altitudes
VALUES
('Activity Monitor', 'PLPOffDrv.sys', 380492.00, 'SK Infosec Co');
INSERT INTO dbo.filter_driver_altitudes
VALUES
('Activity Monitor', 'ISFPDrv.sys', 380491.00, 'SK Infosec Co');
INSERT INTO dbo.filter_driver_altitudes
VALUES
('Activity Monitor', 'ionmonwdrv.sys', 380490.00, 'SK Infosec Co');
INSERT INTO dbo.filter_driver_altitudes
VALUES
('Activity Monitor', 'Sefo.sys - Middle', 380480.00, 'Solusseum Inc');
INSERT INTO dbo.filter_driver_altitudes
VALUES
('Activity Monitor', 'sagntflt.sys', 380470.00, 'ShinNihonSystec Co');
INSERT INTO dbo.filter_driver_altitudes
VALUES
('Activity Monitor', 'VrVBRFsFilter.sys', 380461.00, 'Hauri Inc');
INSERT INTO dbo.filter_driver_altitudes
VALUES
('Activity Monitor', 'VrExpDrv.sys', 380460.00, 'Hauri Inc');
INSERT INTO dbo.filter_driver_altitudes
VALUES
('Activity Monitor', 'srminifilterdrv.sys', 380450.00, 'Citrix Systems');
INSERT INTO dbo.filter_driver_altitudes
VALUES
('Activity Monitor', 'zzpensys.sys', 380440.00, 'Zhuan Zhuan Jing Shen');
INSERT INTO dbo.filter_driver_altitudes
VALUES
('Activity Monitor', 'tedrdrv.sys', 380430.00, 'Palo Alto Networks');
INSERT INTO dbo.filter_driver_altitudes
VALUES
('Activity Monitor', 'fangcloud_autolock_driver.sys', 380420.00, 'Hangzhou Yifangyun');
INSERT INTO dbo.filter_driver_altitudes
VALUES
('Activity Monitor', 'FASDriver', 380410.00, 'Tech Research');
INSERT INTO dbo.filter_driver_altitudes
VALUES
('Activity Monitor', 'kFileFlt.sys', 380405.00, 'AsiaInfo Technologies');
INSERT INTO dbo.filter_driver_altitudes
VALUES
('Activity Monitor', 'cpfd10.sys', 380400.00, 'CYEBIZ co Ltd');
INSERT INTO dbo.filter_driver_altitudes
VALUES
('Activity Monitor', 'ZeroneAODVirtualDisk.sys', 380390.50, 'Zero One Technology Co');
INSERT INTO dbo.filter_driver_altitudes
VALUES
('Activity Monitor', 'ZeroneAODVirtualDisk64.sys', 380390.50, 'Zero One Technology Co');
INSERT INTO dbo.filter_driver_altitudes
VALUES
('Activity Monitor', 'CbSampleDrv.sys', 380020.00, 'Microsoft');
INSERT INTO dbo.filter_driver_altitudes
VALUES
('Activity Monitor', 'CbSampleDrv.sys', 380010.00, 'Microsoft');
INSERT INTO dbo.filter_driver_altitudes
VALUES
('Activity Monitor', 'CbSampleDrv.sys', 380000.00, 'Microsoft');
INSERT INTO dbo.filter_driver_altitudes
VALUES
('Activity Monitor', 'EdsAppRep.sys', 372000.50, 'Alibaba Cloud Computing Ltd.');
INSERT INTO dbo.filter_driver_altitudes
VALUES
('Activity Monitor', 'simrep.sys', 371100.00, 'Microsoft');
INSERT INTO dbo.filter_driver_altitudes
VALUES
('Activity Monitor', 'change.sys', 370160.00, 'Microsoft');
INSERT INTO dbo.filter_driver_altitudes
VALUES
('Activity Monitor', 'delete_flt.sys', 370150.00, 'Microsoft');
INSERT INTO dbo.filter_driver_altitudes
VALUES
('Activity Monitor', 'SmbResilFilter.sys', 370140.00, 'Microsoft');
INSERT INTO dbo.filter_driver_altitudes
VALUES
('Activity Monitor', 'usbtest.sys', 370130.00, 'Microsoft');
INSERT INTO dbo.filter_driver_altitudes
VALUES
('Activity Monitor', 'NameChanger.sys', 370120.00, 'Microsoft');
INSERT INTO dbo.filter_driver_altitudes
VALUES
('Activity Monitor', 'failMount.sys', 370110.00, 'Microsoft');
INSERT INTO dbo.filter_driver_altitudes
VALUES
('Activity Monitor', 'failAttach.sys', 370100.00, 'Microsoft');
INSERT INTO dbo.filter_driver_altitudes
VALUES
('Activity Monitor', 'stest.sys', 370090.00, 'Microsoft');
INSERT INTO dbo.filter_driver_altitudes
VALUES
('Activity Monitor', 'cdo.sys', 370080.00, 'Microsoft');
INSERT INTO dbo.filter_driver_altitudes
VALUES
('Activity Monitor', 'ctx.sys', 370070.00, 'Microsoft');
INSERT INTO dbo.filter_driver_altitudes
VALUES
('Activity Monitor', 'fmm.sys', 370060.00, 'Microsoft');
INSERT INTO dbo.filter_driver_altitudes
VALUES
('Activity Monitor', 'cancelSafe.sys', 370050.00, 'Microsoft');
INSERT INTO dbo.filter_driver_altitudes
VALUES
('Activity Monitor', 'message.sys', 370040.00, 'Microsoft');
INSERT INTO dbo.filter_driver_altitudes
VALUES
('Activity Monitor', 'passThrough.sys', 370030.00, 'Microsoft');
INSERT INTO dbo.filter_driver_altitudes
VALUES
('Activity Monitor', 'nullFilter.sys', 370020.00, 'Microsoft');
INSERT INTO dbo.filter_driver_altitudes
VALUES
('Activity Monitor', 'ntest.sys', 370010.00, 'Microsoft');
INSERT INTO dbo.filter_driver_altitudes
VALUES
('Activity Monitor', 'minispy.sys - Middle', 370000.00, 'Microsoft');
INSERT INTO dbo.filter_driver_altitudes
VALUES
('Activity Monitor', 'KenestoDriveAC.sys', 369620.50, 'Kenesto Corp');
INSERT INTO dbo.filter_driver_altitudes
VALUES
('Activity Monitor', 'ZdProtect.sys', 369600.50, 'Chongqing Intelligent Information Tech Co.,Ltd');
INSERT INTO dbo.filter_driver_altitudes
VALUES
('Activity Monitor', 'cbfilter20.sys', 369560.50, 'Blondell-Hart Inc.');
INSERT INTO dbo.filter_driver_altitudes
VALUES
('Activity Monitor', 'CyberhavenSystemMonitor.sys', 368550.50, 'Cyberhaven Inc');
INSERT INTO dbo.filter_driver_altitudes
VALUES
('Activity Monitor', 'AvaPsFD.sys', 368540.00, 'Avanite Limited');
INSERT INTO dbo.filter_driver_altitudes
VALUES
('Activity Monitor', 'isecureflt.sys', 368530.00, 'iSecure Ltd.');
INSERT INTO dbo.filter_driver_altitudes
VALUES
('Activity Monitor', 'SFPMonitor.sys - Middle', 368520.00, 'SonicWall Inc');
INSERT INTO dbo.filter_driver_altitudes
VALUES
('Activity Monitor', 'wats_se.sys', 368510.00, 'Fujian Shen Kong');
INSERT INTO dbo.filter_driver_altitudes
VALUES
('Activity Monitor', 'secure_os_mf.sys', 368500.00, 'HAURI');
INSERT INTO dbo.filter_driver_altitudes
VALUES
('Activity Monitor', 'FileMonitor.sys', 368470.00, 'Cygna Labs');
INSERT INTO dbo.filter_driver_altitudes
VALUES
('Activity Monitor', 'asiofms.sys', 368460.00, 'Encourage Technologies');
INSERT INTO dbo.filter_driver_altitudes
VALUES
('Activity Monitor', 'AbtFileSystemBlocker.sys', 368452.00, 'Absolute Software');
INSERT INTO dbo.filter_driver_altitudes
VALUES
('Activity Monitor', 'cbfsfilter2017.sys', 368450.00, 'Absolute Software');
INSERT INTO dbo.filter_driver_altitudes
VALUES
('Activity Monitor', 'FileHubAgent.sys', 368440.00, 'SmartFile LLC');
INSERT INTO dbo.filter_driver_altitudes
VALUES
('Activity Monitor', 'pfracdrv.sys', 368430.00, 'NURILAB');
INSERT INTO dbo.filter_driver_altitudes
VALUES
('Activity Monitor', 'nrcomgrdki.sys', 368420.00, 'NURILAB');
INSERT INTO dbo.filter_driver_altitudes
VALUES
('Activity Monitor', 'nrcomgrdka.sys', 368420.00, 'NURILAB');
INSERT INTO dbo.filter_driver_altitudes
VALUES
('Activity Monitor', 'nrpmonki.sys', 368410.00, 'NURILAB');
INSERT INTO dbo.filter_driver_altitudes
VALUES
('Activity Monitor', 'nrpmonka.sys', 368410.00, 'NURILAB');
INSERT INTO dbo.filter_driver_altitudes
VALUES
('Activity Monitor', 'nravwka.sys', 368400.00, 'NURILAB');
INSERT INTO dbo.filter_driver_altitudes
VALUES
('Activity Monitor', 'bhkavki.sys', 368390.00, 'NURILAB');
INSERT INTO dbo.filter_driver_altitudes
VALUES
('Activity Monitor', 'bhkavka.sys', 368390.00, 'NURILAB');
INSERT INTO dbo.filter_driver_altitudes
VALUES
('Activity Monitor', 'docvmonk.sys', 368380.00, 'NURILAB');
INSERT INTO dbo.filter_driver_altitudes
VALUES
('Activity Monitor', 'docvmonk64.sys', 368380.00, 'NURILAB');
INSERT INTO dbo.filter_driver_altitudes
VALUES
('Activity Monitor', 'InvProtectDrv.sys', 368370.00, 'Invincea');
INSERT INTO dbo.filter_driver_altitudes
VALUES
('Activity Monitor', 'InvProtectDrv64.sys', 368370.00, 'Invincea');
INSERT INTO dbo.filter_driver_altitudes
VALUES
('Activity Monitor', 'browserMon.sys', 368360.00, 'Adtrustmedia');
INSERT INTO dbo.filter_driver_altitudes
VALUES
('Activity Monitor', 'SfdFilter.sys', 368350.00, 'Sandoll Communication');
INSERT INTO dbo.filter_driver_altitudes
VALUES
('Activity Monitor', 'phdcbtdrv.sys', 368340.00, 'PHD Virtual Tech Inc.');
INSERT INTO dbo.filter_driver_altitudes
VALUES
('Activity Monitor', 'sysdiag.sys', 368330.00, 'HeroBravo Technology');
INSERT INTO dbo.filter_driver_altitudes
VALUES
('Activity Monitor', 'wlminisecmod.sys', 368329.00, 'Winicssec Ltd');
INSERT INTO dbo.filter_driver_altitudes
VALUES
('Activity Monitor', 'WntGPDrv.sys', 368327.00, 'Winicssec Ltd');
INSERT INTO dbo.filter_driver_altitudes
VALUES
('Activity Monitor', 'edrdrv.sys', 368325.00, 'Nurd Yazilim A.S.');
INSERT INTO dbo.filter_driver_altitudes
VALUES
('Activity Monitor', 'CmdCwagt.sys', 368322.00, 'Comodo Security Solutions Inc.');
INSERT INTO dbo.filter_driver_altitudes
VALUES
('Activity Monitor', 'cfrmd.sys', 368320.00, 'Comodo Security Solutions Inc.');
INSERT INTO dbo.filter_driver_altitudes
VALUES
('Activity Monitor', 'repdrv.sys', 368310.00, 'Vision Solutions');
INSERT INTO dbo.filter_driver_altitudes
VALUES
('Activity Monitor', 'repmon.sys', 368300.00, 'Vision Solutions');
INSERT INTO dbo.filter_driver_altitudes
VALUES
('Activity Monitor', 'cvofflineFlt32.sys', 368200.00, 'Quantum Corporation.');
INSERT INTO dbo.filter_driver_altitudes
VALUES
('Activity Monitor', 'cvofflineFlt64.sys', 368200.00, 'Quantum Corporation.');
INSERT INTO dbo.filter_driver_altitudes
VALUES
('Activity Monitor', 'DsDriver.sys', 368100.00, 'Warp Disk Software');
INSERT INTO dbo.filter_driver_altitudes
VALUES
('Activity Monitor', 'xdrmon.sys', 368050.50, 'LLC Breakthrough Technologies');
INSERT INTO dbo.filter_driver_altitudes
VALUES
('Activity Monitor', 'nlcbhelpx86.sys', 368000.00, 'NetLib');
INSERT INTO dbo.filter_driver_altitudes
VALUES
('Activity Monitor', 'nlcbhelpx64.sys', 368000.00, 'NetLib');
INSERT INTO dbo.filter_driver_altitudes
VALUES
('Activity Monitor', 'nlcbhelpi64.sys', 368000.00, 'NetLib');
INSERT INTO dbo.filter_driver_altitudes
VALUES
('Activity Monitor', 'wbfilter.sys', 367950.00, 'Whitebox Security');
INSERT INTO dbo.filter_driver_altitudes
VALUES
('Activity Monitor', 'LRAgentMF.sys', 367900.00, 'LogRhythm Inc.');
INSERT INTO dbo.filter_driver_altitudes
VALUES
('Activity Monitor', 'Drwebfwflt.sys', 367810.00, 'Doctor Web');
INSERT INTO dbo.filter_driver_altitudes
VALUES
('Activity Monitor', 'EventMon.sys', 367800.00, 'Doctor Web');
COMMIT TRAN;
GO
BEGIN TRAN;
INSERT INTO dbo.filter_driver_altitudes
VALUES
('Activity Monitor', 'dsfltfs.sys', 367760.00, 'Digitalsense Co');
INSERT INTO dbo.filter_driver_altitudes
VALUES
('Activity Monitor', 'soidriver.sys', 367750.00, 'Sophos Plc');
INSERT INTO dbo.filter_driver_altitudes
VALUES
('Activity Monitor', 'drvhookcsmf.sys', 367700.00, 'GrammaTech, Inc.');
INSERT INTO dbo.filter_driver_altitudes
VALUES
('Activity Monitor', 'drvhookcsmf_amd64.sys', 367700.00, 'GrammaTech, Inc.');
INSERT INTO dbo.filter_driver_altitudes
VALUES
('Activity Monitor', 'RevoNetDriver.sys', 367650.00, 'J''s Communication Co.');
INSERT INTO dbo.filter_driver_altitudes
VALUES
('Activity Monitor', 'avipbb.sys', 367600.00, 'Avira GmbH');
INSERT INTO dbo.filter_driver_altitudes
VALUES
('Activity Monitor', 'FileSightMF.sys', 367500.00, 'PA File Sight');
INSERT INTO dbo.filter_driver_altitudes
VALUES
('Activity Monitor', 'csaam.sys', 367400.00, 'Cisco Systems');
INSERT INTO dbo.filter_driver_altitudes
VALUES
('Activity Monitor', 'FSMon.sys', 367300.00, '1mill');
INSERT INTO dbo.filter_driver_altitudes
VALUES
('Activity Monitor', 'AccessValidator.sys', 367200.00, 'Shanghai YiCun Network Tech Co. Ltd');
INSERT INTO dbo.filter_driver_altitudes
VALUES
('Activity Monitor', 'filefilter.sys', 367100.00, 'Beijing Zhong Hang Jiaxin Computer Technology Co.,Ltd.');
INSERT INTO dbo.filter_driver_altitudes
VALUES
('Activity Monitor', 'iiscache.sys', 367000.00, 'Microsoft');
INSERT INTO dbo.filter_driver_altitudes
VALUES
('Activity Monitor', 'nowonmf.sys', 366993.00, 'Diskeeper Corporation');
INSERT INTO dbo.filter_driver_altitudes
VALUES
('Activity Monitor', 'dktlfsmf.sys', 366992.00, 'Diskeeper Corporation');
INSERT INTO dbo.filter_driver_altitudes
VALUES
('Activity Monitor', 'DKDrv.sys', 366991.00, 'Diskeeper Corporation');
INSERT INTO dbo.filter_driver_altitudes
VALUES
('Activity Monitor', 'DKRtWrt.sys - temp fix for XPSP3', 366990.00, 'Diskeeper Corporation');
INSERT INTO dbo.filter_driver_altitudes
VALUES
('Activity Monitor', 'HBFSFltr.sys', 366980.00, 'Diskeeper Corporation');
INSERT INTO dbo.filter_driver_altitudes
VALUES
('Activity Monitor', 'xoiv8x64.sys', 366940.00, 'Arcserve');
INSERT INTO dbo.filter_driver_altitudes
VALUES
('Activity Monitor', 'xomfcbt8x64.sys', 366930.00, 'CA');
INSERT INTO dbo.filter_driver_altitudes
VALUES
('Activity Monitor', 'KmxAgent.sys', 366920.00, 'CA');
INSERT INTO dbo.filter_driver_altitudes
VALUES
('Activity Monitor', 'KmxFile.sys', 366910.00, 'CA');
INSERT INTO dbo.filter_driver_altitudes
VALUES
('Activity Monitor', 'KmxSbx.sys', 366900.00, 'CA');
INSERT INTO dbo.filter_driver_altitudes
VALUES
('Activity Monitor', 'PointGuardVistaR32.sys', 366810.00, 'Futuresoft');
INSERT INTO dbo.filter_driver_altitudes
VALUES
('Activity Monitor', 'PointGuardVistaR64.sys', 366810.00, 'Futuresoft');
INSERT INTO dbo.filter_driver_altitudes
VALUES
('Activity Monitor', 'PointGuardVistaF.sys', 366800.00, 'Futuresoft');
INSERT INTO dbo.filter_driver_altitudes
VALUES
('Activity Monitor', 'PointGuardVista64F.sys', 366800.00, 'Futuresoft');
INSERT INTO dbo.filter_driver_altitudes
VALUES
('Activity Monitor', 'vintmfs.sys', 366789.00, 'CondusivTechnologies');
INSERT INTO dbo.filter_driver_altitudes
VALUES
('Activity Monitor', 'hiofs.sys', 366782.00, 'Condusiv Technologies');
INSERT INTO dbo.filter_driver_altitudes
VALUES
('Activity Monitor', 'intmfs.sys', 366781.00, 'CondusivTechnologies');
INSERT INTO dbo.filter_driver_altitudes
VALUES
('Activity Monitor', 'excfs.sys', 366780.00, 'CondusivTechnologies');
INSERT INTO dbo.filter_driver_altitudes
VALUES
('Activity Monitor', 'zampit_ml.sys', 366700.00, 'Zampit');
INSERT INTO dbo.filter_driver_altitudes
VALUES
('Activity Monitor', 'ACE-BASE.sys', 366669.60, 'Tencent Technology');
INSERT INTO dbo.filter_driver_altitudes
VALUES
('Activity Monitor', 'ACE-GAME.sys', 366669.50, 'Tencent Technology');
INSERT INTO dbo.filter_driver_altitudes
VALUES
('Activity Monitor', 'TenRSafe2.sys', 366669.00, 'Tencent Technology');
INSERT INTO dbo.filter_driver_altitudes
VALUES
('Activity Monitor', 'tesxporter.sys', 366667.00, 'Tencent Technology');
INSERT INTO dbo.filter_driver_altitudes
VALUES
('Activity Monitor', 'tesxnginx.sys', 366666.00, 'Tencent Technology');
INSERT INTO dbo.filter_driver_altitudes
VALUES
('Activity Monitor', 'detector.sys', 366620.50, 'MemCrypt Limited');
INSERT INTO dbo.filter_driver_altitudes
VALUES
('Activity Monitor', 'rflog.sys', 366600.00, 'AppStream, Inc.');
INSERT INTO dbo.filter_driver_altitudes
VALUES
('Activity Monitor', 'csmon.sys', 366582.00, 'CyberSight Inc');
INSERT INTO dbo.filter_driver_altitudes
VALUES
('Activity Monitor', 'mumdi.sys', 366540, 'ZenmuTech Inc.');
INSERT INTO dbo.filter_driver_altitudes
VALUES
('Activity Monitor', 'LivedriveFilter.sys', 366500, 'Livedrive Internet Ltd');
INSERT INTO dbo.filter_driver_altitudes
VALUES
('Activity Monitor', 'regmonex.sys', 366410, 'Tranxition Corp');
INSERT INTO dbo.filter_driver_altitudes
VALUES
('Activity Monitor', 'TXRegMon.sys', 366400, 'Tranxition Corp');
INSERT INTO dbo.filter_driver_altitudes
VALUES
('Activity Monitor', 'SDVFilter.sys', 366300, 'Soliton Systems K.K.');
INSERT INTO dbo.filter_driver_altitudes
VALUES
('Activity Monitor', 'eLock2FSCTLDriver.sys', 366210, 'Egis Technology Inc.');
INSERT INTO dbo.filter_driver_altitudes
VALUES
('Activity Monitor', 'msiodrv4.sys', 366200, 'Centennial Software Ltd');
INSERT INTO dbo.filter_driver_altitudes
VALUES
('Activity Monitor', 'mmPsy32.sys', 366110, 'Resplendence Software Projects');
INSERT INTO dbo.filter_driver_altitudes
VALUES
('Activity Monitor', 'mmPsy64.sys', 366110, 'Resplendence Software Projects');
INSERT INTO dbo.filter_driver_altitudes
VALUES
('Activity Monitor', 'rrMon32.sys', 366100, 'Resplendence Software Projects');
INSERT INTO dbo.filter_driver_altitudes
VALUES
('Activity Monitor', 'rrMon64.sys', 366100, 'Resplendence Software Projects');
INSERT INTO dbo.filter_driver_altitudes
VALUES
('Activity Monitor', 'cvsflt.sys', 366000, 'March Hare Software Ltd');
INSERT INTO dbo.filter_driver_altitudes
VALUES
('Activity Monitor', 'ktsyncfsflt.sys', 365920, 'KnowledgeTree Inc.');
INSERT INTO dbo.filter_driver_altitudes
VALUES
('Activity Monitor', 'nvmon.sys', 365900, 'NetVision, Inc.');
INSERT INTO dbo.filter_driver_altitudes
VALUES
('Activity Monitor', 'SnDacs.sys', 365810, 'Informzaschita');
INSERT INTO dbo.filter_driver_altitudes
VALUES
('Activity Monitor', 'SnExequota.sys', 365800, 'Informzaschita');
INSERT INTO dbo.filter_driver_altitudes
VALUES
('Activity Monitor', 'llfilter.sys', 365700, 'SecureAxis Software');
INSERT INTO dbo.filter_driver_altitudes
VALUES
('Activity Monitor', 'hafsnk.sys', 365660, 'HA Unix Pt');
INSERT INTO dbo.filter_driver_altitudes
VALUES
('Activity Monitor', 'DgeDriver.sys', 365655, 'Dell Software Inc.');
INSERT INTO dbo.filter_driver_altitudes
VALUES
('Activity Monitor', 'BWFSDrv.sys', 365650, 'Quest Software Inc.');
INSERT INTO dbo.filter_driver_altitudes
VALUES
('Activity Monitor', 'CAADFlt.sys', 365601, 'Quest Software Inc.');
INSERT INTO dbo.filter_driver_altitudes
VALUES
('Activity Monitor', 'QFAPFlt.sys', 365600, 'Quest Software');
INSERT INTO dbo.filter_driver_altitudes
VALUES
('Activity Monitor', 'XendowFLT.sys', 365570, 'Credant Technologies');
INSERT INTO dbo.filter_driver_altitudes
VALUES
('Activity Monitor', 'fmdrive.sys', 365500, 'Cigital, Inc.');
INSERT INTO dbo.filter_driver_altitudes
VALUES
('Activity Monitor', 'EGMinFlt.sys', 365400, 'WhiteCell Software Inc.');
INSERT INTO dbo.filter_driver_altitudes
VALUES
('Activity Monitor', 'it2reg.sys', 365315, 'Soliton Systems');
INSERT INTO dbo.filter_driver_altitudes
VALUES
('Activity Monitor', 'it2drv.sys', 365310, 'Soliton Systems');
INSERT INTO dbo.filter_driver_altitudes
VALUES
('Activity Monitor', 'solitkm.sys', 365300, 'Soliton Systems');
INSERT INTO dbo.filter_driver_altitudes
VALUES
('Activity Monitor', 'pgpwdefs.sys', 365270, 'Symantec');
INSERT INTO dbo.filter_driver_altitudes
VALUES
('Activity Monitor', 'GEProtection.sys', 365260, 'Symantec');
INSERT INTO dbo.filter_driver_altitudes
VALUES
('Activity Monitor', 'diflt.sys', 365260, 'Symantec Corp.');
INSERT INTO dbo.filter_driver_altitudes
VALUES
('Activity Monitor', 'sysMon.sys', 365250, 'Symantec');
INSERT INTO dbo.filter_driver_altitudes
VALUES
('Activity Monitor', 'ssrfsf.sys', 365210, 'Symantec');
INSERT INTO dbo.filter_driver_altitudes
VALUES
('Activity Monitor', 'emxdrv2.sys', 365200, 'Symantec');
INSERT INTO dbo.filter_driver_altitudes
VALUES
('Activity Monitor', 'reghook.sys', 365150, 'Symantec');
INSERT INTO dbo.filter_driver_altitudes
VALUES
('Activity Monitor', 'spbbcdrv.sys', 365100, 'Symantec');
INSERT INTO dbo.filter_driver_altitudes
VALUES
('Activity Monitor', 'bhdrvx86.sys', 365100, 'Symantec');
INSERT INTO dbo.filter_driver_altitudes
VALUES
('Activity Monitor', 'bhdrvx64.sys', 365100, 'Symantec');
INSERT INTO dbo.filter_driver_altitudes
VALUES
('Activity Monitor', 'symevnt.sys', 365090, 'Broadcom');
INSERT INTO dbo.filter_driver_altitudes
VALUES
('Activity Monitor', 'symevnt32.sys', 365090, 'Broadcom');
INSERT INTO dbo.filter_driver_altitudes
VALUES
('Activity Monitor', 'SISIPSFileFilter', 365010, 'Symantec');
INSERT INTO dbo.filter_driver_altitudes
VALUES
('Activity Monitor', 'symevent.sys', 365000, 'Symantec');
INSERT INTO dbo.filter_driver_altitudes
VALUES
('Activity Monitor', 'BHDrvx64.sys', 364970, 'NortonLifeLock Inc');
INSERT INTO dbo.filter_driver_altitudes
VALUES
('Activity Monitor', 'BHDrvx86.sys', 364970, 'NortonLifeLock Inc');
INSERT INTO dbo.filter_driver_altitudes
VALUES
('Activity Monitor', 'symevnt.sys', 364960, 'NortonLifeLock Inc');
INSERT INTO dbo.filter_driver_altitudes
VALUES
('Activity Monitor', 'symevnt32.sys', 364960, 'NortonLifeLock Inc');
INSERT INTO dbo.filter_driver_altitudes
VALUES
('Activity Monitor', 'SymEvent.sys', 364950, 'NortonLifeLock Inc');
INSERT INTO dbo.filter_driver_altitudes
VALUES
('Activity Monitor', 'wrpfv.sys', 364900, 'Microsoft');
INSERT INTO dbo.filter_driver_altitudes
VALUES
('Activity Monitor', 'UpGuardRealTime.sys', 364810, 'UpGuard');
INSERT INTO dbo.filter_driver_altitudes
VALUES
('Activity Monitor', 'usbl_ifsfltr.sys', 364800, 'SecureAxis');
INSERT INTO dbo.filter_driver_altitudes
VALUES
('Activity Monitor', 'ntfsf.sys', 364700, 'Sun&Moon Rise');
INSERT INTO dbo.filter_driver_altitudes
VALUES
('Activity Monitor', 'BssAudit.sys', 364600, 'ByStorm');
INSERT INTO dbo.filter_driver_altitudes
VALUES
('Activity Monitor', 'GPMiniFIlter.sys', 364500, 'Kalpataru');
INSERT INTO dbo.filter_driver_altitudes
VALUES
('Activity Monitor', 'AlfaFF.sys', 364400, 'Alfa');
INSERT INTO dbo.filter_driver_altitudes
VALUES
('Activity Monitor', '360disproc.sys', 364310.5, '360 Software (Beijing)');
INSERT INTO dbo.filter_driver_altitudes
VALUES
('Activity Monitor', 'FSAFilter.sys', 364300, 'ScriptLogic');
INSERT INTO dbo.filter_driver_altitudes
VALUES
('Activity Monitor', 'GcfFilter.sys', 364200, 'GemacmbH');
INSERT INTO dbo.filter_driver_altitudes
VALUES
('Activity Monitor', 'FFCFILT.SYS', 364100, 'FFC Limited');
INSERT INTO dbo.filter_driver_altitudes
VALUES
('Activity Monitor', 'msnfsflt.sys', 364000, 'Microsoft');
INSERT INTO dbo.filter_driver_altitudes
VALUES
('Activity Monitor', 'mblmon.sys', 363900, 'Packeteer');
INSERT INTO dbo.filter_driver_altitudes
VALUES
('Activity Monitor', 'amsfilter.sys', 363800, 'Axur Information Sec.');
INSERT INTO dbo.filter_driver_altitudes
VALUES
('Activity Monitor', 'rswctrl.sys', 363713, 'Douzone Bizon Co');
INSERT INTO dbo.filter_driver_altitudes
VALUES
('Activity Monitor', 'mcstrg.sys', 363712, 'Douzone Bizon Co');
INSERT INTO dbo.filter_driver_altitudes
VALUES
('Activity Monitor', 'fmkkc.sys', 363711, 'Douzone Bizon Co');
INSERT INTO dbo.filter_driver_altitudes
VALUES
('Activity Monitor', 'nmlhssrv01.sys', 363710, 'Douzone Bizon Co');
INSERT INTO dbo.filter_driver_altitudes
VALUES
('Activity Monitor', 'equ8_helper.sys', 363705, 'Int3 Software AB');
INSERT INTO dbo.filter_driver_altitudes
VALUES
('Activity Monitor', 'strapvista.sys (retired)', 363700, 'AvSoft Technologies');
COMMIT TRAN;
GO
BEGIN TRAN;
INSERT INTO dbo.filter_driver_altitudes
VALUES
('Activity Monitor', 'SAFE-Agent.sys', 363636, 'SAFE-Cyberdefense');
INSERT INTO dbo.filter_driver_altitudes
VALUES
('Activity Monitor', 'EstPrmon.sys', 363610, 'ESTsoft corp.');
INSERT INTO dbo.filter_driver_altitudes
VALUES
('Activity Monitor', 'Estprp.sys - 64bit', 363610, 'ESTsoft corp.');
INSERT INTO dbo.filter_driver_altitudes
VALUES
('Activity Monitor', 'EstRegmon.sys', 363600, 'ESTsoft corp.');
INSERT INTO dbo.filter_driver_altitudes
VALUES
('Activity Monitor', 'EstRegp.sys - 64bit', 363600, 'ESTsoft corp.');
INSERT INTO dbo.filter_driver_altitudes
VALUES
('Activity Monitor', 'EMAC-Driver-x64.sys', 363570, 'EMAC LAB');
INSERT INTO dbo.filter_driver_altitudes
VALUES
('Activity Monitor', 'HuntMon.sys', 363558.5, 'Huntress Labs Inc');
INSERT INTO dbo.filter_driver_altitudes
VALUES
('Activity Monitor', 'agfsmon.sys', 363530, 'TechnoKom Ltd.');
INSERT INTO dbo.filter_driver_altitudes
VALUES
('Activity Monitor', 'NlxFF.sys', 363520, 'OnGuard Systems LLC');
INSERT INTO dbo.filter_driver_altitudes
VALUES
('Activity Monitor', 'Sahara.sys', 363511, 'Safend');
INSERT INTO dbo.filter_driver_altitudes
VALUES
('Activity Monitor', 'Santa.sys', 363510, 'Safend');
INSERT INTO dbo.filter_driver_altitudes
VALUES
('Activity Monitor', 'vfdrv.sys', 363500, 'Viewfinity');
INSERT INTO dbo.filter_driver_altitudes
VALUES
('Activity Monitor', 'topdogfsfilt.sys', 363450, 'ManTech');
INSERT INTO dbo.filter_driver_altitudes
VALUES
('Activity Monitor', 'mamflt.sys', 363430, 'Mirekusoft LLC');
INSERT INTO dbo.filter_driver_altitudes
VALUES
('Activity Monitor', 'sfac.sys', 363420, 'SoulFrost');
INSERT INTO dbo.filter_driver_altitudes
VALUES
('Activity Monitor', 'xhunter64.sys', 363400, 'Wellbia.com');
INSERT INTO dbo.filter_driver_altitudes
VALUES
('Activity Monitor', 'uncheater.sys', 363390, 'Wellbia.com');
INSERT INTO dbo.filter_driver_altitudes
VALUES
('Activity Monitor', 'AuditFlt.sys', 363313, 'Ionx Solutions LLP');
INSERT INTO dbo.filter_driver_altitudes
VALUES
('Activity Monitor', 'SPIMiniFilter.sys', 363300, 'Software Pursuits Inc.');
INSERT INTO dbo.filter_driver_altitudes
VALUES
('Activity Monitor', 'EAAntiCheat.sys', 363250, 'Electronic Arts');
INSERT INTO dbo.filter_driver_altitudes
VALUES
('Activity Monitor', 'RevBitsESMF.sys', 363240.5, 'RevBits LLC');
INSERT INTO dbo.filter_driver_altitudes
VALUES
('Activity Monitor', 'mracdrv.sys', 363230, 'Mail․Ru');
INSERT INTO dbo.filter_driver_altitudes
VALUES
('Activity Monitor', 'BEDaisy.sys', 363220, 'BattlEye Innovations');
INSERT INTO dbo.filter_driver_altitudes
VALUES
('Activity Monitor', 'MPKernel.sys', 363210, 'Lovelace Network Tech');
INSERT INTO dbo.filter_driver_altitudes
VALUES
('Activity Monitor', 'NetAccCtrl.sys', 363200, 'LINK co., ltd');
INSERT INTO dbo.filter_driver_altitudes
VALUES
('Activity Monitor', 'NetAccCtrl64.sys', 363200, 'LINK co., ltd');
INSERT INTO dbo.filter_driver_altitudes
VALUES
('Activity Monitor', 'bzsenedrsysdrv.sys', 363143, 'BiZone LLC');
INSERT INTO dbo.filter_driver_altitudes
VALUES
('Activity Monitor', 'bzsenyaradrv.sys', 363142, 'BiZone LLC');
INSERT INTO dbo.filter_driver_altitudes
VALUES
('Activity Monitor', 'bzsenspdrv.sys', 363141, 'BiZone LLC');
INSERT INTO dbo.filter_driver_altitudes
VALUES
('Activity Monitor', 'bzsenth.sys', 363140, 'BiZone LLC');
INSERT INTO dbo.filter_driver_altitudes
VALUES
('Activity Monitor', 'hpreg.sys', 363130, 'HP');
INSERT INTO dbo.filter_driver_altitudes
VALUES
('Activity Monitor', 'QMON.sys', 363122, 'Qualys Inc.');
INSERT INTO dbo.filter_driver_altitudes
VALUES
('Activity Monitor', 'qfimdvr.sys', 363120, 'Qualys Inc.');
INSERT INTO dbo.filter_driver_altitudes
VALUES
('Activity Monitor', 'QDocumentREF.sys', 363110, 'BicDroid Inc.');
INSERT INTO dbo.filter_driver_altitudes
VALUES
('Activity Monitor', 'dsfemon.sys', 363100, 'Topology Ltd');
INSERT INTO dbo.filter_driver_altitudes
VALUES
('Activity Monitor', 'AmznMon.sys', 363030, 'Amazon Web Services Inc');
INSERT INTO dbo.filter_driver_altitudes
VALUES
('Activity Monitor', 'iothorfs.sys', 363020, 'ioScience');
INSERT INTO dbo.filter_driver_altitudes
VALUES
('Activity Monitor', 'ctamflt.sys', 363010, 'ComTrade');
INSERT INTO dbo.filter_driver_altitudes
VALUES
('Activity Monitor', 'psisolator.sys', 363000, 'SharpCrafters');
INSERT INTO dbo.filter_driver_altitudes
VALUES
('Activity Monitor', 'QmInspec.sys', 362990, 'Beijing QiAnXin Tech.');
INSERT INTO dbo.filter_driver_altitudes
VALUES
('Activity Monitor', 'HVLMinifilter.sys', 362980, 'HAVELSAN A.Ş');
INSERT INTO dbo.filter_driver_altitudes
VALUES
('Activity Monitor', 'GagSecurity.sys', 362970, 'Beijing Shu Yan Science');
INSERT INTO dbo.filter_driver_altitudes
VALUES
('Activity Monitor', 'vfpd.sys', 362962, 'CyberArk Software');
INSERT INTO dbo.filter_driver_altitudes
VALUES
('Activity Monitor', 'CybKernelTracker.sys', 362960, 'CyberArk Software');
INSERT INTO dbo.filter_driver_altitudes
VALUES
('Activity Monitor', 'filemon.sys', 362950, 'Temasoft S.R.L.');
INSERT INTO dbo.filter_driver_altitudes
VALUES
('Activity Monitor', 'SCAegis.sys', 362940, 'Sogou Ltd.');
INSERT INTO dbo.filter_driver_altitudes
VALUES
('Activity Monitor', 'fpepflt.sys', 362930, 'ForcePoint LLC.');
INSERT INTO dbo.filter_driver_altitudes
VALUES
('Activity Monitor', 'GTVService.sys', 362920, 'GTV VIETNAM TECHNOLOGY');
INSERT INTO dbo.filter_driver_altitudes
VALUES
('Activity Monitor', 'RWLog1.sys', 362910.5, 'ROMWin Co. Ltd');
INSERT INTO dbo.filter_driver_altitudes
VALUES
('Activity Monitor', 'klifks.sys', 362902, 'Kaspersky Lab');
INSERT INTO dbo.filter_driver_altitudes
VALUES
('Activity Monitor', 'klifaa.sys', 362901, 'Kaspersky Lab');
INSERT INTO dbo.filter_driver_altitudes
VALUES
('Activity Monitor', 'Klifsm.sys', 362900, 'Kaspersky Lab');
INSERT INTO dbo.filter_driver_altitudes
VALUES
('Activity Monitor', 'lwdcs.sys', 362880.5, 'Lacework Inc');
INSERT INTO dbo.filter_driver_altitudes
VALUES
('Activity Monitor', 'Spotlight.sys', 362870, 'Cigent Technology Inc');
INSERT INTO dbo.filter_driver_altitudes
VALUES
('Activity Monitor', 'nxrmflt.sys', 362860, 'NextLabs, Inc.');
INSERT INTO dbo.filter_driver_altitudes
VALUES
('Activity Monitor', 'vast.sys', 362850, 'EclecticIQ BV');
INSERT INTO dbo.filter_driver_altitudes
VALUES
('Activity Monitor', 'AALProtect.sys', 362840, 'AlphaAntiLeak');
INSERT INTO dbo.filter_driver_altitudes
VALUES
('Activity Monitor', 'egnfsflt.sys', 362830, 'Egnyte Inc');
INSERT INTO dbo.filter_driver_altitudes
VALUES
('Activity Monitor', 'RsFlt.sys', 362820, 'Redstor Limited');
INSERT INTO dbo.filter_driver_altitudes
VALUES
('Activity Monitor', 'CentrifyFSF.sys', 362810, 'Centrify Corp');
INSERT INTO dbo.filter_driver_altitudes
VALUES
('Activity Monitor', 'Sefo.sys - Bottom', 362800, 'Solusseum Inc');
INSERT INTO dbo.filter_driver_altitudes
VALUES
('Activity Monitor', 'proggerdriver.sys', 362790, 'WaikatoLink Ltd');
INSERT INTO dbo.filter_driver_altitudes
VALUES
('Activity Monitor', 'imfilter.sys', 362780, 'ITsMine');
INSERT INTO dbo.filter_driver_altitudes
VALUES
('Activity Monitor', 'webargus.sys', 362775.5, 'Digital Information Technologies');
INSERT INTO dbo.filter_driver_altitudes
VALUES
('Activity Monitor', 'DSHSM.sys', 362770.5, 'DeepSpace Storage Systems');
INSERT INTO dbo.filter_driver_altitudes
VALUES
('Activity Monitor', 'GraphiteSecureDriver.sys', 362750.5, 'Towers Watson Software');
INSERT INTO dbo.filter_driver_altitudes
VALUES
('Activity Monitor', 'IndigoSecureDriver.sys', 362750, 'Towers Watson Software');
INSERT INTO dbo.filter_driver_altitudes
VALUES
('Activity Monitor', 'SFPMonitor.sys - Bottom', 362700, 'SonicWall Inc');
INSERT INTO dbo.filter_driver_altitudes
VALUES
('Activity Monitor', 'minispy.sys - Bottom', 361000, 'Microsoft');
INSERT INTO dbo.filter_driver_altitudes
VALUES
('Undelete', 'BSSFlt.sys', 346000, 'Blue Shoe Software LLC');
INSERT INTO dbo.filter_driver_altitudes
VALUES
('Undelete', 'ThinIO.sys', 345900, 'ThinScale Technology');
INSERT INTO dbo.filter_driver_altitudes
VALUES
('Undelete', 'hmpalert.sys', 345800, 'SurfRight');
INSERT INTO dbo.filter_driver_altitudes
VALUES
('Undelete', 'nsffkmd64.sys', 345700, 'NetSTAR Inc.');
INSERT INTO dbo.filter_driver_altitudes
VALUES
('Undelete', 'nsffkmd32.sys', 345700, 'NetSTAR Inc.');
INSERT INTO dbo.filter_driver_altitudes
VALUES
('Undelete', 'xbprocfilter.sys', 345600, 'Zrxb');
INSERT INTO dbo.filter_driver_altitudes
VALUES
('Undelete', 'ifileguard.sys', 345500, 'I-O DATA DEVICE, INC.');
INSERT INTO dbo.filter_driver_altitudes
VALUES
('Undelete', 'undelex32.sys', 345400, 'Resplendence Software Projects');
INSERT INTO dbo.filter_driver_altitudes
VALUES
('Undelete', 'undelex64.sys', 345400, 'Resplendence Software Projects');
INSERT INTO dbo.filter_driver_altitudes
VALUES
('Undelete', 'starmon.sys', 345300, 'Kantowitz Engineering, Inc.');
INSERT INTO dbo.filter_driver_altitudes
VALUES
('Undelete', 'mxRCycle.sys', 345200, 'Avanquest');
INSERT INTO dbo.filter_driver_altitudes
VALUES
('Undelete', 'UdFilter.sys', 345100, 'Diskeeper Corporation');
INSERT INTO dbo.filter_driver_altitudes
VALUES
('Undelete', 'it2prtc.sys', 345040, 'Soliton Systems K.K.');
INSERT INTO dbo.filter_driver_altitudes
VALUES
('Undelete', 'SolRegFilter.sys', 345030, 'Soliton Systems K.K.');
INSERT INTO dbo.filter_driver_altitudes
VALUES
('Undelete', 'SolSecBr.sys', 345020, 'Soliton Systems K.K.');
INSERT INTO dbo.filter_driver_altitudes
VALUES
('Undelete', 'SolFCLLi.sys', 345010, 'Soliton Systems K.K.');
INSERT INTO dbo.filter_driver_altitudes
VALUES
('Undelete', 'WinSetupMon.sys', 345102, 'Microsoft');
INSERT INTO dbo.filter_driver_altitudes
VALUES
('Undelete', 'SolFCL.sys', 345000, 'Soliton Smart Sec');
INSERT INTO dbo.filter_driver_altitudes
VALUES
('Undelete', 'DCVPr.sys', 340700, 'SecurStar GmbH');
INSERT INTO dbo.filter_driver_altitudes
VALUES
('Anti-Virus', 'brynhildr.sys', 329400, 'Activision Blizzard, Inc.');
INSERT INTO dbo.filter_driver_altitudes
VALUES
('Anti-Virus', 'IstroDrv.sys', 329380.5, 'IstroSec s.r.o.');
INSERT INTO dbo.filter_driver_altitudes
VALUES
('Anti-Virus', 'XRFilter.sys', 329375, 'XRITDX');
INSERT INTO dbo.filter_driver_altitudes
VALUES
('Anti-Virus', 'tbmninifilter.sys', 329370, 'Confluera Inc');
INSERT INTO dbo.filter_driver_altitudes
VALUES
('Anti-Virus', 'CovAgent.sys', 329365, 'Field Effect Software');
INSERT INTO dbo.filter_driver_altitudes
VALUES
('Anti-Virus', 'trfsfilter.sys', 329360, 'NetSecurity Corp');
INSERT INTO dbo.filter_driver_altitudes
VALUES
('Anti-Virus', 'ReveFltMgr.sys', 329350, 'REVE Antivirus');
INSERT INTO dbo.filter_driver_altitudes
VALUES
('Anti-Virus', 'ReveProcProtection.sys', 329340, 'REVE Antivirus');
INSERT INTO dbo.filter_driver_altitudes
VALUES
('Anti-Virus', 'zwPxeSvr.sys', 329330, 'SecureLink Inc.');
INSERT INTO dbo.filter_driver_altitudes
VALUES
('Anti-Virus', 'zwASatom.sys', 329320, 'SecureLink Inc.');
INSERT INTO dbo.filter_driver_altitudes
VALUES
('Anti-Virus', 'wscm.sys', 329310, 'Fujitsu Social Science');
INSERT INTO dbo.filter_driver_altitudes
VALUES
('Anti-Virus', 'IMFFilter.sys', 329300, 'IObit Information Tech');
INSERT INTO dbo.filter_driver_altitudes
VALUES
('Anti-Virus', 'CSFlt.sys', 329290, 'ConeSecurity Inc');
INSERT INTO dbo.filter_driver_altitudes
VALUES
('Anti-Virus', 'PWProtect.sys', 329250, 'PerfectWorld Ltd');
INSERT INTO dbo.filter_driver_altitudes
VALUES
('Anti-Virus', 'Osiris.sys', 329240, 'Binary Defense Systems');
INSERT INTO dbo.filter_driver_altitudes
VALUES
('Anti-Virus', 'ospfile_mini.sys', 329230, 'OKUMA Corp');
INSERT INTO dbo.filter_driver_altitudes
VALUES
('Anti-Virus', 'SoftFilterxxx.sys', 329222, 'WidgetNuri Corp');
INSERT INTO dbo.filter_driver_altitudes
VALUES
('Anti-Virus', 'RansomDefensexxx.sys', 329220, 'WidgetNuri Corp');
INSERT INTO dbo.filter_driver_altitudes
VALUES
('Anti-Virus', 'RanPodFS.sys', 329210, 'Pooyan System');
INSERT INTO dbo.filter_driver_altitudes
VALUES
('Anti-Virus', 'ksfsflt.sys', 329200, 'Beijing Kingsoft');
INSERT INTO dbo.filter_driver_altitudes
VALUES
('Anti-Virus', 'DeepInsFS.sys', 329190, 'Deep Instinct');
INSERT INTO dbo.filter_driver_altitudes
VALUES
('Anti-Virus', 'AppCheckD.sys', 329180, 'CheckMAL Inc');
INSERT INTO dbo.filter_driver_altitudes
VALUES
('Anti-Virus', 'spellmon.sys', 329170, 'SpellSecurity');
INSERT INTO dbo.filter_driver_altitudes
VALUES
('Anti-Virus', 'WhiteShield.sys', 329160, 'Meidensha Corp');
INSERT INTO dbo.filter_driver_altitudes
VALUES
('Anti-Virus', 'reaqtor.sys', 329150, 'ReaQta Ltd.');
INSERT INTO dbo.filter_driver_altitudes
VALUES
('Anti-Virus', 'SE46Filter.sys', 329140, 'Technology Nexus AB');
INSERT INTO dbo.filter_driver_altitudes
VALUES
('Anti-Virus', 'FileScan.sys', 329130, 'NPcore Ltd');
INSERT INTO dbo.filter_driver_altitudes
VALUES
('Anti-Virus', 'ECATDriver.sys', 329120, 'EMC');
INSERT INTO dbo.filter_driver_altitudes
VALUES
('Anti-Virus', 'pfkrnl.sys', 329110, 'FXSEC LTD');
INSERT INTO dbo.filter_driver_altitudes
VALUES
('Anti-Virus', 'epicFilter.sys', 329100, 'Hidden Reflex');
INSERT INTO dbo.filter_driver_altitudes
VALUES
('Anti-Virus', 'EdnemFsFilter.sys', 329090, 'Dakota State University');
INSERT INTO dbo.filter_driver_altitudes
VALUES
('Anti-Virus', 'b9kernel.sys', 329050, 'Bit9 Inc');
INSERT INTO dbo.filter_driver_altitudes
VALUES
('Anti-Virus', 'eeCtrl.sys', 329010, 'symantec');
INSERT INTO dbo.filter_driver_altitudes
VALUES
('Anti-Virus', 'eraser.sys (Retired)', 329010, 'symantec');
INSERT INTO dbo.filter_driver_altitudes
VALUES
('Anti-Virus', 'SRTSP.sys', 329000, 'symantec');
INSERT INTO dbo.filter_driver_altitudes
VALUES
('Anti-Virus', 'SRTSPIT.sys - ia64 systems', 329000, 'symantec');
INSERT INTO dbo.filter_driver_altitudes
VALUES
('Anti-Virus', 'SRTSP64.SYS - x64 systems', 329000, 'symantec');
INSERT INTO dbo.filter_driver_altitudes
VALUES
('Anti-Virus', 'eeCtrl.sys', 328960, 'NortonLifeLock Inc');
INSERT INTO dbo.filter_driver_altitudes
VALUES
('Anti-Virus', 'SRTSP.sys', 328950, 'NortonLifeLock Inc');
INSERT INTO dbo.filter_driver_altitudes
VALUES
('Anti-Virus', 'SRTSP64.sys', 328950, 'NortonLifeLock Inc');
INSERT INTO dbo.filter_driver_altitudes
VALUES
('Anti-Virus', 'a2ertpx86.sys', 328920, 'Emsi Software GmbH');
INSERT INTO dbo.filter_driver_altitudes
VALUES
('Anti-Virus', 'a2ertpx64.sys', 328920, 'Emsi Software GmbH');
INSERT INTO dbo.filter_driver_altitudes
VALUES
('Anti-Virus', 'a2gffx86.sys - x86', 328910, 'Emsi Software GmbH');
INSERT INTO dbo.filter_driver_altitudes
VALUES
('Anti-Virus', 'a2gffx64.sys - x64', 328910, 'Emsi Software GmbH');
INSERT INTO dbo.filter_driver_altitudes
VALUES
('Anti-Virus', 'a2gffi64.sys - IA64', 328910, 'Emsi Software GmbH');
INSERT INTO dbo.filter_driver_altitudes
VALUES
('Anti-Virus', 'a2acc.sys', 328900, 'Emsi Software GmbH');
INSERT INTO dbo.filter_driver_altitudes
VALUES
('Anti-Virus', 'a2acc64.sys on x64 systems', 328900, 'Emsi Software GmbH');
INSERT INTO dbo.filter_driver_altitudes
VALUES
('Anti-Virus', 'FlightRecorder.sys', 328850, 'Malwarebytes Corp.');
INSERT INTO dbo.filter_driver_altitudes
VALUES
('Anti-Virus', 'upfilt.sys', 328820.5, 'Upsight Security');
INSERT INTO dbo.filter_driver_altitudes
VALUES
('Anti-Virus', 'si32_file.sys', 328810, 'Scargo Inc');
INSERT INTO dbo.filter_driver_altitudes
VALUES
('Anti-Virus', 'si64_file.sys', 328810, 'Scargo Inc');
INSERT INTO dbo.filter_driver_altitudes
VALUES
('Anti-Virus', 'mbam.sys', 328800, 'Malwarebytes Corp.');
INSERT INTO dbo.filter_driver_altitudes
VALUES
('Anti-Virus', 'lnvscenter.sys', 328780, 'Lenovo');
COMMIT TRAN;
GO
BEGIN TRAN;
INSERT INTO dbo.filter_driver_altitudes
VALUES
('Anti-Virus', 'EnigmaFileMonDriver.sys', 328770, 'EnigmaSoft');
INSERT INTO dbo.filter_driver_altitudes
VALUES
('Anti-Virus', 'KUBWKSP.sys', 328750, 'Netlor SAS');
INSERT INTO dbo.filter_driver_altitudes
VALUES
('Anti-Virus', 'hcp_kernel_acq.sys', 328740, 'refractionPOINT');
INSERT INTO dbo.filter_driver_altitudes
VALUES
('Anti-Virus', 'SegiraFlt.sys', 328730, 'Segira LLC');
INSERT INTO dbo.filter_driver_altitudes
VALUES
('Anti-Virus', 'wdocsafe.sys', 328722, 'Cheetah Mobile Inc.');
INSERT INTO dbo.filter_driver_altitudes
VALUES
('Anti-Virus', 'lbprotect.sys', 328720, 'Cheetah Mobile Inc.');
INSERT INTO dbo.filter_driver_altitudes
VALUES
('Anti-Virus', 'eamonm.sys', 328700, 'ESET, spol. s r.o.');
INSERT INTO dbo.filter_driver_altitudes
VALUES
('Anti-Virus', 'snpavdrv.sys', 328660, 'Security Code LLC');
INSERT INTO dbo.filter_driver_altitudes
VALUES
('Anti-Virus', 'klam.sys', 328650, 'Kaspersky Lab');
INSERT INTO dbo.filter_driver_altitudes
VALUES
('Anti-Virus', 'MaxProc64.sys', 328620, 'Max Secure Software');
INSERT INTO dbo.filter_driver_altitudes
VALUES
('Anti-Virus', 'MaxProtector.sys', 328610, 'Max Secure Software');
INSERT INTO dbo.filter_driver_altitudes
VALUES
('Anti-Virus', 'maxcryptmon.sys', 328601, 'Max Secure Software');
INSERT INTO dbo.filter_driver_altitudes
VALUES
('Anti-Virus', 'SDActMon.sys', 328600, 'Max Secure Software');
INSERT INTO dbo.filter_driver_altitudes
VALUES
('Anti-Virus', 'TmKmSnsr.sys', 328550, 'Trend Micro Inc.');
INSERT INTO dbo.filter_driver_altitudes
VALUES
('Anti-Virus', 'fileflt.sys', 328540, 'Trend Micro Inc.');
INSERT INTO dbo.filter_driver_altitudes
VALUES
('Anti-Virus', 'TmEsFlt.sys', 328530, 'Trend Micro Inc.');
INSERT INTO dbo.filter_driver_altitudes
VALUES
('Anti-Virus', 'TmEyes.sys', 328520, 'Trend Micro Inc.');
INSERT INTO dbo.filter_driver_altitudes
VALUES
('Anti-Virus', 'tmevtmgr.sys', 328510, 'Trend Micro Inc.');
INSERT INTO dbo.filter_driver_altitudes
VALUES
('Anti-Virus', 'tmpreflt.sys', 328500, 'Trend');
INSERT INTO dbo.filter_driver_altitudes
VALUES
('Anti-Virus', 'vcMFilter.sys', 328400, 'SGRI Co., LTD.');
INSERT INTO dbo.filter_driver_altitudes
VALUES
('Anti-Virus', 'SAFsFilter.sys', 328300, 'Lightspeed Systems Inc.');
INSERT INTO dbo.filter_driver_altitudes
VALUES
('Anti-Virus', 'vsepflt.sys', 328200, 'VMware, Inc.');
INSERT INTO dbo.filter_driver_altitudes
VALUES
('Anti-Virus', 'VFileFilter.sys(renamed)', 328200, 'VMware, Inc.');
INSERT INTO dbo.filter_driver_altitudes
VALUES
('Anti-Virus', 'sfavflt.sys', 328130, 'Sangfor Technologies');
INSERT INTO dbo.filter_driver_altitudes
VALUES
('Anti-Virus', 'sfavflt.sys', 328120, 'Sangfor Technologies');
INSERT INTO dbo.filter_driver_altitudes
VALUES
('Anti-Virus', 'drivesentryfilterdriver2lite.sys', 328100, 'DriveSentry Inc');
INSERT INTO dbo.filter_driver_altitudes
VALUES
('Anti-Virus', 'WdFilter.sys', 328010, 'Microsoft');
INSERT INTO dbo.filter_driver_altitudes
VALUES
('Anti-Virus', 'mpFilter.sys', 328000, 'Microsoft');
INSERT INTO dbo.filter_driver_altitudes
VALUES
('Anti-Virus', 'vrSDetri.sys', 327801, 'ETRI');
INSERT INTO dbo.filter_driver_altitudes
VALUES
('Anti-Virus', 'vrSDetrix.sys', 327800, 'ETRI');
INSERT INTO dbo.filter_driver_altitudes
VALUES
('Anti-Virus', 'AhkSvPro.sys', 327720, 'Ahkun Co., Ltd.');
INSERT INTO dbo.filter_driver_altitudes
VALUES
('Anti-Virus', 'AhkUsbFW.sys', 327710, 'Ahkun Co., Ltd.');
INSERT INTO dbo.filter_driver_altitudes
VALUES
('Anti-Virus', 'AhkAMFlt.sys', 327700, 'Ahkun Co., Ltd.');
INSERT INTO dbo.filter_driver_altitudes
VALUES
('Anti-Virus', 'majoradvapi.sys', 327680, 'Beijing Majorsec');
INSERT INTO dbo.filter_driver_altitudes
VALUES
('Anti-Virus', 'PSINPROC.SYS', 327620, 'Panda Security');
INSERT INTO dbo.filter_driver_altitudes
VALUES
('Anti-Virus', 'PSINFILE.SYS', 327610, 'Panda Security');
INSERT INTO dbo.filter_driver_altitudes
VALUES
('Anti-Virus', 'amfsm.sys - Windows XP/2003 x64', 327600, 'Panda Security');
INSERT INTO dbo.filter_driver_altitudes
VALUES
('Anti-Virus', 'amm8660.sys - Windows Vista x86', 327600, 'Panda Security');
INSERT INTO dbo.filter_driver_altitudes
VALUES
('Anti-Virus', 'amm6460.sys - Windows Vista x64', 327600, 'Panda Security');
INSERT INTO dbo.filter_driver_altitudes
VALUES
('Anti-Virus', 'PerfectWorldAntiCheatSys.sys', 327560, 'Perfect World Co. Ltd');
INSERT INTO dbo.filter_driver_altitudes
VALUES
('Anti-Virus', 'ADSpiderDoc.sys', 327550, 'Digitalonnet');
INSERT INTO dbo.filter_driver_altitudes
VALUES
('Anti-Virus', 'BkavAutoFlt.sys', 327542, 'Bkav Corporation');
INSERT INTO dbo.filter_driver_altitudes
VALUES
('Anti-Virus', 'BkavSdFlt.sys', 327540, 'Bkav Corporation');
INSERT INTO dbo.filter_driver_altitudes
VALUES
('Anti-Virus', 'easyanticheat.sys', 327530, 'EasyAntiCheat Solutions');
INSERT INTO dbo.filter_driver_altitudes
VALUES
('Anti-Virus', '5nine.cbt.sys', 327520, '5nine Software Inc.');
INSERT INTO dbo.filter_driver_altitudes
VALUES
('Anti-Virus', 'caavFltr.sys', 327510, 'Computer Assoc');
INSERT INTO dbo.filter_driver_altitudes
VALUES
('Anti-Virus', 'ino_fltr.sys', 327500, 'Computer Assoc');
INSERT INTO dbo.filter_driver_altitudes
VALUES
('Anti-Virus', 'SECOne_USB.sys', 327426, 'GRGBanking Equipment');
INSERT INTO dbo.filter_driver_altitudes
VALUES
('Anti-Virus', 'SECOne_Proc10.sys', 327424, 'GRGBanking Equipment');
INSERT INTO dbo.filter_driver_altitudes
VALUES
('Anti-Virus', 'SECOne_REG10.sys', 327422, 'GRGBanking Equipment');
INSERT INTO dbo.filter_driver_altitudes
VALUES
('Anti-Virus', 'SECOne_FileMon10.sys', 327420, 'GRGBanking Equipment');
INSERT INTO dbo.filter_driver_altitudes
VALUES
('Anti-Virus', 'WCSDriver.sys', 327410, 'White Cloud Security');
INSERT INTO dbo.filter_driver_altitudes
VALUES
('Anti-Virus', '360qpesv.sys', 327404, '360 Software (Beijing)');
INSERT INTO dbo.filter_driver_altitudes
VALUES
('Anti-Virus', 'dsark.sys', 327402, 'Qihoo 360');
INSERT INTO dbo.filter_driver_altitudes
VALUES
('Anti-Virus', '360avflt.sys', 327400, 'Qihoo 360');
INSERT INTO dbo.filter_driver_altitudes
VALUES
('Anti-Virus', 'sciptflt.sys', 327334, 'SECUI Corporation');
INSERT INTO dbo.filter_driver_altitudes
VALUES
('Anti-Virus', 'scifsflt.sys', 327333, 'SECUI Corporation');
INSERT INTO dbo.filter_driver_altitudes
VALUES
('Anti-Virus', 'ANVfsm.sys', 327310, 'Arcdo');
INSERT INTO dbo.filter_driver_altitudes
VALUES
('Anti-Virus', 'CDrRSFlt.sys', 327300, 'Arcdo');
INSERT INTO dbo.filter_driver_altitudes
VALUES
('Anti-Virus', 'mfdriver.sys', 327250, 'Imperva Inc.');
INSERT INTO dbo.filter_driver_altitudes
VALUES
('Anti-Virus', 'EPSMn.sys', 327200, 'SGA');
INSERT INTO dbo.filter_driver_altitudes
VALUES
('Anti-Virus', 'TxFileFilter.sys', 327160, 'Beijing Venus');
INSERT INTO dbo.filter_driver_altitudes
VALUES
('Anti-Virus', 'VTSysFlt.sys', 327150, 'Beijing Venus');
INSERT INTO dbo.filter_driver_altitudes
VALUES
('Anti-Virus', 'TesMon.sys', 327130, 'Tencent');
INSERT INTO dbo.filter_driver_altitudes
VALUES
('Anti-Virus', 'QQSysMonX64.sys', 327125, 'Tencent');
INSERT INTO dbo.filter_driver_altitudes
VALUES
('Anti-Virus', 'QQSysMon.sys', 327120, 'Tencent');
INSERT INTO dbo.filter_driver_altitudes
VALUES
('Anti-Virus', 'TSysCare.sys', 327110, 'Shenzhen Tencent Computer Systems Company Limited');
INSERT INTO dbo.filter_driver_altitudes
VALUES
('Anti-Virus', 'TFsFlt.sys', 327100, 'Shenzhen Tencent Computer Systems Company Limited');
INSERT INTO dbo.filter_driver_altitudes
VALUES
('Anti-Virus', 'avmf.sys', 327000, 'Authentium');
INSERT INTO dbo.filter_driver_altitudes
VALUES
('Anti-Virus', 'BDFileDefend.sys', 326916, 'Baidu (beijing)');
INSERT INTO dbo.filter_driver_altitudes
VALUES
('Anti-Virus', 'BDsdKit.sys', 326914, 'Baidu online network technology (beijing)Co., Ltd');
INSERT INTO dbo.filter_driver_altitudes
VALUES
('Anti-Virus', 'bd0003.sys', 326912, 'Baidu online network technology (beijing)Co., Ltd');
INSERT INTO dbo.filter_driver_altitudes
VALUES
('Anti-Virus', 'Bfilter.sys', 326910, 'Baidu (Hong Kong) Limited');
INSERT INTO dbo.filter_driver_altitudes
VALUES
('Anti-Virus', 'NeoKerbyFilter', 326900, 'NeoAutus');
INSERT INTO dbo.filter_driver_altitudes
VALUES
('Anti-Virus', 'egnfsflt.sys', 326830, 'Egnyte Inc');
INSERT INTO dbo.filter_driver_altitudes
VALUES
('Anti-Virus', 'RsFlt.sys', 326820, 'Redstor Limited');
INSERT INTO dbo.filter_driver_altitudes
VALUES
('Anti-Virus', 'trpmnflt.sys', 326810, 'TRAPMINE A.S.');
INSERT INTO dbo.filter_driver_altitudes
VALUES
('Anti-Virus', 'PLGFltr.sys', 326800, 'Paretologic');
INSERT INTO dbo.filter_driver_altitudes
VALUES
('Anti-Virus', 'WrdWizSecure64.sys', 326730, 'WardWiz');
INSERT INTO dbo.filter_driver_altitudes
VALUES
('Anti-Virus', 'wrdwizscanner.sys', 326720, 'WardWiz');
INSERT INTO dbo.filter_driver_altitudes
VALUES
('Anti-Virus', 'AshAvScan.sys', 326700, 'Ashampoo GmbH & Co. KG');
INSERT INTO dbo.filter_driver_altitudes
VALUES
('Anti-Virus', 'Zyfm.sys', 326666, 'ZhengYong InfoTech LTD.');
INSERT INTO dbo.filter_driver_altitudes
VALUES
('Anti-Virus', 'csaav.sys', 326600, 'Cisco Systems');
INSERT INTO dbo.filter_driver_altitudes
VALUES
('Anti-Virus', 'oavfm.sys', 326550, 'HSM IT-Services Gmbh');
INSERT INTO dbo.filter_driver_altitudes
VALUES
('Anti-Virus', 'SegMD.sys', 326520, 'Segurmatica');
INSERT INTO dbo.filter_driver_altitudes
VALUES
('Anti-Virus', 'SegMP.sys', 326510, 'Segurmatica');
INSERT INTO dbo.filter_driver_altitudes
VALUES
('Anti-Virus', 'SegF.sys', 326500, 'Segurmatica');
INSERT INTO dbo.filter_driver_altitudes
VALUES
('Anti-Virus', 'eeyehv.sys', 326400, 'eEye Digital Security');
INSERT INTO dbo.filter_driver_altitudes
VALUES
('Anti-Virus', 'eeyehv64.sys', 326400, 'eEye Digital Security');
INSERT INTO dbo.filter_driver_altitudes
VALUES
('Anti-Virus', 'CpAvFilter.sys', 326311, 'CodeProof Technologies Inc');
INSERT INTO dbo.filter_driver_altitudes
VALUES
('Anti-Virus', 'CpAvKernel.sys', 326310, 'CodeProof Technologies Inc');
INSERT INTO dbo.filter_driver_altitudes
VALUES
('Anti-Virus', 'NovaShield.sys', 326300, 'Securitas Technologies,Inc.');
INSERT INTO dbo.filter_driver_altitudes
VALUES
('Anti-Virus', 'SheedAntivirusFilterDriver.sys', 326290, 'SheedSoft Ltd');
INSERT INTO dbo.filter_driver_altitudes
VALUES
('Anti-Virus', 'bSyirmf.sys', 326260, 'BLACKFORT SECURITY');
INSERT INTO dbo.filter_driver_altitudes
VALUES
('Anti-Virus', 'bSysp.sys', 326250, 'BLACKFORT SECURITY');
INSERT INTO dbo.filter_driver_altitudes
VALUES
('Anti-Virus', 'bSydf.sys', 326240, 'BLACKFORT SECURITY');
INSERT INTO dbo.filter_driver_altitudes
VALUES
('Anti-Virus', 'bSywl.sys', 326235, 'BLACKFORT SECURITY');
INSERT INTO dbo.filter_driver_altitudes
VALUES
('Anti-Virus', 'bSyrtm.sys', 326230, 'BLACKFORT SECURITY');
INSERT INTO dbo.filter_driver_altitudes
VALUES
('Anti-Virus', 'bSyaed.sys', 326220, 'BLACKFORT SECURITY');
INSERT INTO dbo.filter_driver_altitudes
VALUES
('Anti-Virus', 'bSyar.sys', 326210, 'BLACKFORT SECURITY');
INSERT INTO dbo.filter_driver_altitudes
VALUES
('Anti-Virus', 'BdFileSpy.sys', 326200, 'BullGuard');
INSERT INTO dbo.filter_driver_altitudes
VALUES
('Anti-Virus', 'npxgd.sys', 326160, 'INCA Internet Co., Ltd');
INSERT INTO dbo.filter_driver_altitudes
VALUES
('Anti-Virus', 'npxgd64.sys', 326160, 'INCA Internet Co., Ltd');
INSERT INTO dbo.filter_driver_altitudes
VALUES
('Anti-Virus', 'tkpl2k.sys', 326150, 'INCA Internet Co., Ltd');
INSERT INTO dbo.filter_driver_altitudes
VALUES
('Anti-Virus', 'tkpl2k64.sys', 326150, 'INCA Internet Co., Ltd');
INSERT INTO dbo.filter_driver_altitudes
VALUES
('Anti-Virus', 'GKFF.sys', 326140, 'INCA Internet Co., Ltd');
INSERT INTO dbo.filter_driver_altitudes
VALUES
('Anti-Virus', 'GKFF64.sys', 326140, 'INCA Internet Co., Ltd');
INSERT INTO dbo.filter_driver_altitudes
VALUES
('Anti-Virus', 'tkdac2k.sys', 326130, 'INCA Internet Co., Ltd');
INSERT INTO dbo.filter_driver_altitudes
VALUES
('Anti-Virus', 'tkdacxp.sys', 326130, 'INCA Internet Co., Ltd');
INSERT INTO dbo.filter_driver_altitudes
VALUES
('Anti-Virus', 'tkdacxp64.sys', 326130, 'INCA Internet Co., Ltd');
INSERT INTO dbo.filter_driver_altitudes
VALUES
('Anti-Virus', 'tksp2k.sys', 326120, 'INCA Internet Co., Ltd');
INSERT INTO dbo.filter_driver_altitudes
VALUES
('Anti-Virus', 'tkspxp.sys', 326120, 'INCA Internet Co., Ltd');
INSERT INTO dbo.filter_driver_altitudes
VALUES
('Anti-Virus', 'tkspxp64.sys', 326120, 'INCA Internet Co., Ltd');
INSERT INTO dbo.filter_driver_altitudes
VALUES
('Anti-Virus', 'tkfsft.sys', 326110, 'INCA Internet Co., Ltd');
INSERT INTO dbo.filter_driver_altitudes
VALUES
('Anti-Virus', 'tkfsft64.sys - 64bit', 326110, 'INCA Internet Co., Ltd');
INSERT INTO dbo.filter_driver_altitudes
VALUES
('Anti-Virus', 'tkfsavxp.sys - 32bit', 326100, 'INCA Internet Co., Ltd');
INSERT INTO dbo.filter_driver_altitudes
VALUES
('Anti-Virus', 'tkfsavxp64.sys - 64bit', 326100, 'INCA Internet Co., Ltd');
INSERT INTO dbo.filter_driver_altitudes
VALUES
('Anti-Virus', 'SMDrvNt.sys', 326040, 'AhnLab, Inc.');
INSERT INTO dbo.filter_driver_altitudes
VALUES
('Anti-Virus', 'ATamptNt.sys', 326030, 'AhnLab, Inc.');
INSERT INTO dbo.filter_driver_altitudes
VALUES
('Anti-Virus', 'V3Flt2k.sys', 326020, 'AhnLab, Inc.');
INSERT INTO dbo.filter_driver_altitudes
VALUES
('Anti-Virus', 'V3MifiNt.sys', 326010, 'Ahnlab');
INSERT INTO dbo.filter_driver_altitudes
VALUES
('Anti-Virus', 'V3Ift2k.sys', 326000, 'Ahnlab');
INSERT INTO dbo.filter_driver_altitudes
VALUES
('Anti-Virus', 'V3IftmNt.sys (Old name)', 326000, 'Ahnlab');
INSERT INTO dbo.filter_driver_altitudes
VALUES
('Anti-Virus', 'ArfMonNt.sys', 325990, 'Ahnlab');
INSERT INTO dbo.filter_driver_altitudes
VALUES
('Anti-Virus', 'AhnRghLh.sys', 325980, 'Ahnlab');
INSERT INTO dbo.filter_driver_altitudes
VALUES
('Anti-Virus', 'AszFltNt.sys', 325970, 'Ahnlab');
INSERT INTO dbo.filter_driver_altitudes
VALUES
('Anti-Virus', 'OMFltLh.sys', 325960, 'Ahnlab');
INSERT INTO dbo.filter_driver_altitudes
VALUES
('Anti-Virus', 'V3Flu2k.sys', 325950, 'Ahnlab');
INSERT INTO dbo.filter_driver_altitudes
VALUES
('Anti-Virus', 'TfFregNt.sys', 325940, 'AhnLab Inc.');
INSERT INTO dbo.filter_driver_altitudes
VALUES
('Anti-Virus', 'AdcVcsNT.sys', 325930, 'Ahnlab');
INSERT INTO dbo.filter_driver_altitudes
VALUES
('Anti-Virus', 'vcdriv.sys', 325820, 'Greatsoft Corp.Ltd');
INSERT INTO dbo.filter_driver_altitudes
VALUES
('Anti-Virus', 'vcreg.sys', 325810, 'Greatsoft Corp.Ltd');
INSERT INTO dbo.filter_driver_altitudes
VALUES
('Anti-Virus', 'vchle.sys', 325800, 'Greatsoft Corp.Ltd');
INSERT INTO dbo.filter_driver_altitudes
VALUES
('Anti-Virus', 'NxFsMon.sys', 325700, 'Novatix Corporation');
INSERT INTO dbo.filter_driver_altitudes
VALUES
('Anti-Virus', 'LiveGuardAntiCheat.sys', 325650, 'LiveGuard Software Ltd.');
INSERT INTO dbo.filter_driver_altitudes
VALUES
('Anti-Virus', 'AntiLeakFilter.sys', 325600, 'Individual developer (Soft3304)');
INSERT INTO dbo.filter_driver_altitudes
VALUES
('Anti-Virus', 'NanoAVMF.sys', 325510, 'Panda Software');
INSERT INTO dbo.filter_driver_altitudes
VALUES
('Anti-Virus', 'shldflt.sys', 325500, 'Panda Software');
INSERT INTO dbo.filter_driver_altitudes
VALUES
('Anti-Virus', 'nprosec.sys', 325410, 'Norman ASA');
INSERT INTO dbo.filter_driver_altitudes
VALUES
('Anti-Virus', 'nregsec.sys', 325400, 'Norman ASA');
COMMIT TRAN;
GO
BEGIN TRAN;
INSERT INTO dbo.filter_driver_altitudes
VALUES
('Anti-Virus', 'issregistry.sys', 325300, 'IBM');
INSERT INTO dbo.filter_driver_altitudes
VALUES
('Anti-Virus', 'THFilter.sys', 325200, 'Sybonic Systems Inc');
INSERT INTO dbo.filter_driver_altitudes
VALUES
('Anti-Virus', 'pervac.sys', 325100, 'PerSystems SA');
INSERT INTO dbo.filter_driver_altitudes
VALUES
('Anti-Virus', 'avgmfx86.sys', 325000, 'AVG Grisoft');
INSERT INTO dbo.filter_driver_altitudes
VALUES
('Anti-Virus', 'avgmfx64.sys', 325000, 'AVG Grisoft');
INSERT INTO dbo.filter_driver_altitudes
VALUES
('Anti-Virus', 'avgmfi64.sys', 325000, 'AVG Grisoft');
INSERT INTO dbo.filter_driver_altitudes
VALUES
('Anti-Virus', 'avgmfrs.sys (retired)', 325000, 'AVG Grisoft');
INSERT INTO dbo.filter_driver_altitudes
VALUES
('Anti-Virus', 'FortiAptFilter.sys', 324930, 'Fortinet Inc.');
INSERT INTO dbo.filter_driver_altitudes
VALUES
('Anti-Virus', 'fortimon2.sys', 324920, 'Fortinet Inc.');
INSERT INTO dbo.filter_driver_altitudes
VALUES
('Anti-Virus', 'fortirmon.sys', 324910, 'Fortinet Inc.');
INSERT INTO dbo.filter_driver_altitudes
VALUES
('Anti-Virus', 'fortishield.sys', 324900, 'Fortinet Inc.');
INSERT INTO dbo.filter_driver_altitudes
VALUES
('Anti-Virus', 'mscan-rt.sys', 324800, 'SecureBrain Corporation');
INSERT INTO dbo.filter_driver_altitudes
VALUES
('Anti-Virus', 'sysdiag.sys', 324600, 'Huorong Security');
INSERT INTO dbo.filter_driver_altitudes
VALUES
('Anti-Virus', 'agentrtm64.sys', 324510, 'WINS CO. LTD');
INSERT INTO dbo.filter_driver_altitudes
VALUES
('Anti-Virus', 'rswmon.sys', 324500, 'WINS CO. LTD');
INSERT INTO dbo.filter_driver_altitudes
VALUES
('Anti-Virus', 'mwfsmfltr.sys', 324420, 'MicroWorld Software Services Pvt. Ltd.');
INSERT INTO dbo.filter_driver_altitudes
VALUES
('Anti-Virus', 'gtkdrv.sys', 324410, 'GridinSoft LLC');
INSERT INTO dbo.filter_driver_altitudes
VALUES
('Anti-Virus', 'GbpKm.sys', 324400, 'GAS Tecnologia');
INSERT INTO dbo.filter_driver_altitudes
VALUES
('Anti-Virus', 'crnsysm.sys', 324310, 'Coranti Inc.');
INSERT INTO dbo.filter_driver_altitudes
VALUES
('Anti-Virus', 'crncache32.sys', 324300, 'Coranti Inc.');
INSERT INTO dbo.filter_driver_altitudes
VALUES
('Anti-Virus', 'crncache64.sys', 324300, 'Coranti Inc.');
INSERT INTO dbo.filter_driver_altitudes
VALUES
('Anti-Virus', 'egambit.sys', 324242, 'TEHTRI-Security');
INSERT INTO dbo.filter_driver_altitudes
VALUES
('Anti-Virus', 'drwebfwft.sys', 324210, 'Doctor Web');
INSERT INTO dbo.filter_driver_altitudes
VALUES
('Anti-Virus', 'DwShield.sys', 324200, 'Doctor Web');
INSERT INTO dbo.filter_driver_altitudes
VALUES
('Anti-Virus', 'DwShield64.sys', 324200, 'Doctor Web');
INSERT INTO dbo.filter_driver_altitudes
VALUES
('Anti-Virus', 'IProtect.sys', 324150, 'EveryZone Inc.');
INSERT INTO dbo.filter_driver_altitudes
VALUES
('Anti-Virus', 'TvFiltr.sys', 324140, 'EveryZone INC.');
INSERT INTO dbo.filter_driver_altitudes
VALUES
('Anti-Virus', 'TvDriver.sys', 324130, 'EveryZone INC.');
INSERT INTO dbo.filter_driver_altitudes
VALUES
('Anti-Virus', 'TvSPFltr.sys', 324120, 'EveryZone INC.');
INSERT INTO dbo.filter_driver_altitudes
VALUES
('Anti-Virus', 'TvPtFile.sys', 324110, 'EveryZone INC.');
INSERT INTO dbo.filter_driver_altitudes
VALUES
('Anti-Virus', 'TvStFltr.sys', 324101, 'EveryZone INC.');
INSERT INTO dbo.filter_driver_altitudes
VALUES
('Anti-Virus', 'TvMFltr.sys', 324100, 'Everyzone');
INSERT INTO dbo.filter_driver_altitudes
VALUES
('Anti-Virus', 'SophosED.sys', 324050, 'Sophos');
INSERT INTO dbo.filter_driver_altitudes
VALUES
('Anti-Virus', 'SAVOnAccess.sys', 324010, 'Sophos');
INSERT INTO dbo.filter_driver_altitudes
VALUES
('Anti-Virus', 'savonaccess.sys', 324000, 'Sophos');
INSERT INTO dbo.filter_driver_altitudes
VALUES
('Anti-Virus', 'sld.sys', 323990, 'Sophos');
INSERT INTO dbo.filter_driver_altitudes
VALUES
('Anti-Virus', 'OADevice.sys', 323900, 'Tall Emu');
INSERT INTO dbo.filter_driver_altitudes
VALUES
('Anti-Virus', 'pwipf6.sys', 323800, 'PWI, Inc.');
INSERT INTO dbo.filter_driver_altitudes
VALUES
('Anti-Virus', 'EstRkmon.sys', 323700, 'ESTsoft corp.');
INSERT INTO dbo.filter_driver_altitudes
VALUES
('Anti-Virus', 'EstRkr.sys - 64bit', 323700, 'ESTsoft corp.');
INSERT INTO dbo.filter_driver_altitudes
VALUES
('Anti-Virus', 'dwprot.sys', 323610, 'Doctor Web');
INSERT INTO dbo.filter_driver_altitudes
VALUES
('Anti-Virus', 'Spiderg3.sys', 323600, 'Doctor Web Ltd.');
INSERT INTO dbo.filter_driver_altitudes
VALUES
('Anti-Virus', 'STKrnl64.sys', 323500, 'Verdasys Inc');
INSERT INTO dbo.filter_driver_altitudes
VALUES
('Anti-Virus', 'UFDFilter.sys', 323400, 'Yoggie');
INSERT INTO dbo.filter_driver_altitudes
VALUES
('Anti-Virus', 'SCFltr.sys', 323300, 'SecurityCoverage, Inc.');
INSERT INTO dbo.filter_driver_altitudes
VALUES
('Anti-Virus', 'fildds.sys', 323200, 'Filseclab');
INSERT INTO dbo.filter_driver_altitudes
VALUES
('Anti-Virus', 'fsfilter.sys', 323100, 'MastedCode Ltd');
INSERT INTO dbo.filter_driver_altitudes
VALUES
('Anti-Virus', 'fpav_rtp.sys', 323000, 'f-protect');
INSERT INTO dbo.filter_driver_altitudes
VALUES
('Anti-Virus', 'cwdriver.sys', 322900, 'Leith Bade');
INSERT INTO dbo.filter_driver_altitudes
VALUES
('Anti-Virus', 'AYFilter.sys', 322810, 'ESTsoft');
INSERT INTO dbo.filter_driver_altitudes
VALUES
('Anti-Virus', 'Rtw.sys', 322800, 'ESTsoft');
INSERT INTO dbo.filter_driver_altitudes
VALUES
('Anti-Virus', 'EscFilter.sys', 322790.5, 'ESTsecurity Corp');
INSERT INTO dbo.filter_driver_altitudes
VALUES
('Anti-Virus', 'RSRtw.sys', 322790, 'ESTsecurity Corp');
INSERT INTO dbo.filter_driver_altitudes
VALUES
('Anti-Virus', 'RSPCRtw.sys', 322780, 'ESTsecurity Corp');
INSERT INTO dbo.filter_driver_altitudes
VALUES
('Anti-Virus', 'HookSys.sys', 322700, 'Beijing Rising Information Technology Corporation Limited');
INSERT INTO dbo.filter_driver_altitudes
VALUES
('Anti-Virus', 'snscore.sys', 322600, 'S.N.Safe&Software');
INSERT INTO dbo.filter_driver_altitudes
VALUES
('Anti-Virus', 'ssvhook.sys', 322500, 'SecuLution GmbH');
INSERT INTO dbo.filter_driver_altitudes
VALUES
('Anti-Virus', 'strapvista.sys', 322400, 'AvSoft Technologies');
INSERT INTO dbo.filter_driver_altitudes
VALUES
('Anti-Virus', 'strapvista64.sys', 322400, 'AvSoft Technologies');
INSERT INTO dbo.filter_driver_altitudes
VALUES
('Anti-Virus', 'sascan.sys', 322300, 'SecureAge Technology');
INSERT INTO dbo.filter_driver_altitudes
VALUES
('Anti-Virus', 'savant.sys', 322200, 'Savant Protection, Inc.');
INSERT INTO dbo.filter_driver_altitudes
VALUES
('Anti-Virus', 'VrARnFlt.sys', 322161, 'HAURI');
INSERT INTO dbo.filter_driver_altitudes
VALUES
('Anti-Virus', 'VrBBDFlt.sys', 322160, 'HAURI');
INSERT INTO dbo.filter_driver_altitudes
VALUES
('Anti-Virus', 'vrSDfmx.sys', 322153, 'HAURI');
INSERT INTO dbo.filter_driver_altitudes
VALUES
('Anti-Virus', 'vrSDfmx.sys', 322152, 'HAURI');
INSERT INTO dbo.filter_driver_altitudes
VALUES
('Anti-Virus', 'vrSDam.sys', 322151, 'HAURI');
INSERT INTO dbo.filter_driver_altitudes
VALUES
('Anti-Virus', 'vrSDam.sys', 322150, 'HAURI');
INSERT INTO dbo.filter_driver_altitudes
VALUES
('Anti-Virus', 'VRAPTFLT.sys', 322140, 'HAURI Inc.');
INSERT INTO dbo.filter_driver_altitudes
VALUES
('Anti-Virus', 'VrAptDef.sys', 322130, 'HAURI');
INSERT INTO dbo.filter_driver_altitudes
VALUES
('Anti-Virus', 'VrSdCore.sys', 322120, 'HAURI');
INSERT INTO dbo.filter_driver_altitudes
VALUES
('Anti-Virus', 'VrFsFtM.sys', 322110, 'HAURI');
INSERT INTO dbo.filter_driver_altitudes
VALUES
('Anti-Virus', 'VrFsFtMX.sys(AMD64)', 322110, 'HAURI');
INSERT INTO dbo.filter_driver_altitudes
VALUES
('Anti-Virus', 'vradfil2.sys', 322100, 'HAURI');
INSERT INTO dbo.filter_driver_altitudes
VALUES
('Anti-Virus', 'zgflt.sys', 322050, 'ZeroGuard Ltd');
INSERT INTO dbo.filter_driver_altitudes
VALUES
('Anti-Virus', 'fsgk.sys', 322000, 'f-secure');
INSERT INTO dbo.filter_driver_altitudes
VALUES
('Anti-Virus', 'bouncer.sys', 321950, 'CoreTrace Corporation');
INSERT INTO dbo.filter_driver_altitudes
VALUES
('Anti-Virus', 'PCTCore64.sys', 321910, 'PC Tools Pty. Ltd.');
INSERT INTO dbo.filter_driver_altitudes
VALUES
('Anti-Virus', 'PCTCore.sys (Old name)', 321910, 'PC Tools Pty. Ltd.');
INSERT INTO dbo.filter_driver_altitudes
VALUES
('Anti-Virus', 'ikfilesec.sys', 321900, 'PC Tools Pty. Ltd.');
INSERT INTO dbo.filter_driver_altitudes
VALUES
('Anti-Virus', 'ZxFsFilt.sys', 321800, 'Australian Projects');
INSERT INTO dbo.filter_driver_altitudes
VALUES
('Anti-Virus', 'antispyfilter.sys', 321700, 'C-NetMedia Inc');
INSERT INTO dbo.filter_driver_altitudes
VALUES
('Anti-Virus', 'dfndr_am.sys', 321654, 'PSafe Ltd.');
INSERT INTO dbo.filter_driver_altitudes
VALUES
('Anti-Virus', 'hlprotect.sys', 321650, 'HarfangLab');
INSERT INTO dbo.filter_driver_altitudes
VALUES
('Anti-Virus', 'PZDrvXP.sys', 321600, 'VisionPower Co.,Ltd.');
INSERT INTO dbo.filter_driver_altitudes
VALUES
('Anti-Virus', 'haggc.sys', 321510.1, 'Quick Heal Technologies Pvt. Ltd.');
INSERT INTO dbo.filter_driver_altitudes
VALUES
('Anti-Virus', 'ggc.sys', 321510, 'Quick Heal TechnologiesPvt. Ltd.');
INSERT INTO dbo.filter_driver_altitudes
VALUES
('Anti-Virus', 'catflt.sys', 321500, 'Quick Heal TechnologiesPvt. Ltd.');
INSERT INTO dbo.filter_driver_altitudes
VALUES
('Anti-Virus', 'snsrflt.sys', 321495, 'Quick Heal Technologies Pvt. Ltd.');
INSERT INTO dbo.filter_driver_altitudes
VALUES
('Anti-Virus', 'ztflt.sys', 321490.1, 'Quick Heal Technologies Pvt. Ltd');
INSERT INTO dbo.filter_driver_altitudes
VALUES
('Anti-Virus', 'bdsflt.sys', 321490, 'Quick Heal Technologies Pvt. Ltd.');
INSERT INTO dbo.filter_driver_altitudes
VALUES
('Anti-Virus', 'dartflt.sys', 321485, 'Quick Heal Technologies Pvt. Ltd.');
INSERT INTO dbo.filter_driver_altitudes
VALUES
('Anti-Virus', 'arwflt.sys', 321480, 'Quick Heal Technologies Pvt. Ltd.');
INSERT INTO dbo.filter_driver_altitudes
VALUES
('Anti-Virus', 'csagent.sys', 321410, 'CrowdStrike Ltd.');
INSERT INTO dbo.filter_driver_altitudes
VALUES
('Anti-Virus', 'kmkuflt.sys', 321400, 'Komoku Inc.');
INSERT INTO dbo.filter_driver_altitudes
VALUES
('Anti-Virus', 'ntguard.sys', 321337, 'IKARUS Security');
INSERT INTO dbo.filter_driver_altitudes
VALUES
('Anti-Virus', 'epdrv.sys', 321320, 'McAfee Inc.');
INSERT INTO dbo.filter_driver_altitudes
VALUES
('Anti-Virus', 'mfencoas.sys', 321310, 'McAfee Inc.');
INSERT INTO dbo.filter_driver_altitudes
VALUES
('Anti-Virus', 'mfehidk.sys', 321300, 'McAfee Inc.');
INSERT INTO dbo.filter_driver_altitudes
VALUES
('Anti-Virus', 'swin.sys', 321250, 'McAfee Inc.');
INSERT INTO dbo.filter_driver_altitudes
VALUES
('Anti-Virus', 'CyvrFsfd.sys', 321234, 'Palo Alto Networks');
INSERT INTO dbo.filter_driver_altitudes
VALUES
('Anti-Virus', 'cmdccav.sys', 321210, 'Comodo Group Inc.');
INSERT INTO dbo.filter_driver_altitudes
VALUES
('Anti-Virus', 'cmdguard.sys', 321200, 'Comodo Group Inc.');
INSERT INTO dbo.filter_driver_altitudes
VALUES
('Anti-Virus', 'mfesec.sys', 321150.5, 'McAfee, LLC');
INSERT INTO dbo.filter_driver_altitudes
VALUES
('Anti-Virus', 'cbfilter20.sys', 321120.5, 'CMC Cyber Security');
INSERT INTO dbo.filter_driver_altitudes
VALUES
('Anti-Virus', 'cbprocess20.sys', 321120, 'CMC Cyber Security');
INSERT INTO dbo.filter_driver_altitudes
VALUES
('Anti-Virus', 'cbregistry20.sys', 321120, 'CMC Cyber Security');
INSERT INTO dbo.filter_driver_altitudes
VALUES
('Anti-Virus', 'nycu_filter.sys', 321110.5, 'NYCU');
INSERT INTO dbo.filter_driver_altitudes
VALUES
('Anti-Virus', 'K7Sentry.sys', 321100, 'K7 Computing Private Ltd.');
INSERT INTO dbo.filter_driver_altitudes
VALUES
('Anti-Virus', 'nsminflt.sys', 321050, 'NHN');
INSERT INTO dbo.filter_driver_altitudes
VALUES
('Anti-Virus', 'nsminflt64.sys', 321050, 'NHN');
INSERT INTO dbo.filter_driver_altitudes
VALUES
('Anti-Virus', 'nvcmflt.sys', 321000, 'Norman');
INSERT INTO dbo.filter_driver_altitudes
VALUES
('Anti-Virus', 'dgsafe.sys', 320950, 'KINGSOFT');
INSERT INTO dbo.filter_driver_altitudes
VALUES
('Anti-Virus', 'issfltr.sys', 320900, 'ISS');
INSERT INTO dbo.filter_driver_altitudes
VALUES
('Anti-Virus', 'hbflt.sys', 320840, 'BitDefender SRL');
INSERT INTO dbo.filter_driver_altitudes
VALUES
('Anti-Virus', 'vlflt.sys', 320832, 'BitDefender SRL');
INSERT INTO dbo.filter_driver_altitudes
VALUES
('Anti-Virus', 'bdsvm.sys', 320830, 'Bitdefender');
INSERT INTO dbo.filter_driver_altitudes
VALUES
('Anti-Virus', 'gzflt.sys', 320820, 'BitDefender SRL');
INSERT INTO dbo.filter_driver_altitudes
VALUES
('Anti-Virus', 'bddevflt.sys', 320812, 'BitDefender SRL');
INSERT INTO dbo.filter_driver_altitudes
VALUES
('Anti-Virus', 'ignis.sys', 320811, 'BitDefender SRL');
INSERT INTO dbo.filter_driver_altitudes
VALUES
('Anti-Virus', 'AVCKF.SYS', 320810, 'BitDefender SRL');
INSERT INTO dbo.filter_driver_altitudes
VALUES
('Anti-Virus', 'bdfsfltr.sys', 320800, 'Softwin');
INSERT INTO dbo.filter_driver_altitudes
VALUES
('Anti-Virus', 'bdfm.sys', 320790, 'Softwin');
INSERT INTO dbo.filter_driver_altitudes
VALUES
('Anti-Virus', 'gemma.sys', 320782, 'BitDefender SRL');
INSERT INTO dbo.filter_driver_altitudes
VALUES
('Anti-Virus', 'Atc.sys', 320781, 'BitDefender SRL');
INSERT INTO dbo.filter_driver_altitudes
VALUES
('Anti-Virus', 'AVC3.SYS', 320780, 'BitDefender SRL');
INSERT INTO dbo.filter_driver_altitudes
VALUES
('Anti-Virus', 'TRUFOS.SYS', 320770, 'BitDefender SRL');
INSERT INTO dbo.filter_driver_altitudes
VALUES
('Anti-Virus', 'aswmonflt.sys', 320700, 'Alwil');
INSERT INTO dbo.filter_driver_altitudes
VALUES
('Anti-Virus', 'kavnsi.sys', 320650, 'AVNOS');
INSERT INTO dbo.filter_driver_altitudes
VALUES
('Anti-Virus', 'TaegisKM.x64.sys', 320640.5, 'Secureworks Inc.');
INSERT INTO dbo.filter_driver_altitudes
VALUES
('Anti-Virus', 'TaegisKM.x86.sys', 320640.5, 'Secureworks Inc.');
COMMIT TRAN;
GO
BEGIN TRAN;
INSERT INTO dbo.filter_driver_altitudes
VALUES
('Anti-Virus', 'CiscoSAM.sys', 320618, 'Cisco Systems');
INSERT INTO dbo.filter_driver_altitudes
VALUES
('Anti-Virus', 'immunetselfprotect.sys', 320616, 'Cisco Systems');
INSERT INTO dbo.filter_driver_altitudes
VALUES
('Anti-Virus', 'immunetprotect.sys', 320614, 'Cisco Systems');
INSERT INTO dbo.filter_driver_altitudes
VALUES
('Anti-Virus', 'CiscoAMPCEFWDriver.sys', 320612, 'Cisco Systems');
INSERT INTO dbo.filter_driver_altitudes
VALUES
('Anti-Virus', 'CiscoAMPHeurDriver.sys', 320610, 'Cisco Systems');
INSERT INTO dbo.filter_driver_altitudes
VALUES
('Anti-Virus', 'HookCentre.sys', 320602, 'G Data');
INSERT INTO dbo.filter_driver_altitudes
VALUES
('Anti-Virus', 'PktIcpt.sys', 320601, 'G Data');
INSERT INTO dbo.filter_driver_altitudes
VALUES
('Anti-Virus', 'MiniIcpt.sys', 320600, 'G Data');
INSERT INTO dbo.filter_driver_altitudes
VALUES
('Anti-Virus', 'acdrv.sys', 320520, 'OnMoon Company LLC');
INSERT INTO dbo.filter_driver_altitudes
VALUES
('Anti-Virus', 'tmfsdrv2.sys', 320510, 'Teramind');
INSERT INTO dbo.filter_driver_altitudes
VALUES
('Anti-Virus', 'avgntflt.sys', 320500, 'Avira GmbH');
INSERT INTO dbo.filter_driver_altitudes
VALUES
('Anti-Virus', 'klam.sys', 320450, 'Kaspersky Lab');
INSERT INTO dbo.filter_driver_altitudes
VALUES
('Anti-Virus', 'klbg.sys', 320440, 'Kaspersky');
INSERT INTO dbo.filter_driver_altitudes
VALUES
('Anti-Virus', 'kldback.sys', 320430, 'Kaspersky');
INSERT INTO dbo.filter_driver_altitudes
VALUES
('Anti-Virus', 'kldlinf.sys', 320420, 'Kaspersky');
INSERT INTO dbo.filter_driver_altitudes
VALUES
('Anti-Virus', 'kldtool.sys', 320410, 'Kaspersky');
INSERT INTO dbo.filter_driver_altitudes
VALUES
('Anti-Virus', 'klif.sys', 320401, 'Kaspersky Lab');
INSERT INTO dbo.filter_driver_altitudes
VALUES
('Anti-Virus', 'klif.sys', 320400, 'Kaspersky');
INSERT INTO dbo.filter_driver_altitudes
VALUES
('Anti-Virus', 'klam.sys', 320350, 'Kaspersky Lab');
INSERT INTO dbo.filter_driver_altitudes
VALUES
('Anti-Virus', 'hsmltwhl.sys', 320340, 'Hitachi Solutions');
INSERT INTO dbo.filter_driver_altitudes
VALUES
('Anti-Virus', 'hssfwhl.sys', 320330, 'Hitachi Solutions');
INSERT INTO dbo.filter_driver_altitudes
VALUES
('Anti-Virus', 'DeepInsFS.sys', 320323, 'Deep Instinct Ltd.');
INSERT INTO dbo.filter_driver_altitudes
VALUES
('Anti-Virus', 'DeepInsFS.sys', 320322, 'Deep Instinct Ltd.');
INSERT INTO dbo.filter_driver_altitudes
VALUES
('Anti-Virus', 'DeepInsFS.sys', 320321, 'Deep Instinct Ltd.');
INSERT INTO dbo.filter_driver_altitudes
VALUES
('Anti-Virus', 'DeepInsFS.sys', 320320, 'Deep Instinct Ltd.');
INSERT INTO dbo.filter_driver_altitudes
VALUES
('Anti-Virus', 'avfsmn.sys', 320310, 'Anvisoft');
INSERT INTO dbo.filter_driver_altitudes
VALUES
('Anti-Virus', 'lbd.sys', 320300, 'Lavasoft AB');
INSERT INTO dbo.filter_driver_altitudes
VALUES
('Anti-Virus', 'oavnflt.sys', 320250, 'OpenAVN Inc');
INSERT INTO dbo.filter_driver_altitudes
VALUES
('Anti-Virus', 'pavdrv.sys', 320210, 'Panzor Cybersecurity');
INSERT INTO dbo.filter_driver_altitudes
VALUES
('Anti-Virus', 'rvsmon.sys', 320200, 'CJSC Returnil Software');
INSERT INTO dbo.filter_driver_altitudes
VALUES
('Anti-Virus', 'KawachFsMinifilter.sys', 320160, 'Sequretek IT');
INSERT INTO dbo.filter_driver_altitudes
VALUES
('Anti-Virus', 'securoFSD_x64.sys', 320150, 'knowwheresoft Ltd');
INSERT INTO dbo.filter_driver_altitudes
VALUES
('Anti-Virus', 'securoFS.sys', 320149, 'knowwheresoft Ltd');
INSERT INTO dbo.filter_driver_altitudes
VALUES
('Anti-Virus', 'WRAEKernel.sys', 320112, 'Webroot Inc.');
INSERT INTO dbo.filter_driver_altitudes
VALUES
('Anti-Virus', 'WRKrn.sys', 320111, 'Webroot Inc.');
INSERT INTO dbo.filter_driver_altitudes
VALUES
('Anti-Virus', 'WRCore.sys', 320110, 'Webroot Inc.');
INSERT INTO dbo.filter_driver_altitudes
VALUES
('Anti-Virus', 'ssfmonm.sys', 320100, 'Webroot Software, Inc.');
INSERT INTO dbo.filter_driver_altitudes
VALUES
('Anti-Virus', 'ODFsFimFilter.sys', 320070, 'Odyssey Cyber Security');
INSERT INTO dbo.filter_driver_altitudes
VALUES
('Anti-Virus', 'ODFsTokenFilter.sys', 320061, 'Odyssey Cyber Security');
INSERT INTO dbo.filter_driver_altitudes
VALUES
('Anti-Virus', 'ODFsFilter.sys', 320060, 'Odyssey Cyber Security');
INSERT INTO dbo.filter_driver_altitudes
VALUES
('Anti-Virus', 'vk_fsf.sys', 320050, 'AxBx');
INSERT INTO dbo.filter_driver_altitudes
VALUES
('Anti-Virus', 'VirtualAgent.sys', 320005, 'Symantec');
INSERT INTO dbo.filter_driver_altitudes
VALUES
('Replication', 'IntelCAS.sys', 309100, 'Intel Corporation');
INSERT INTO dbo.filter_driver_altitudes
VALUES
('Replication', 'mvfs.sys', 309000, 'IBM Corporation');
INSERT INTO dbo.filter_driver_altitudes
VALUES
('Replication', 'frxccd.sys', 306000, 'FSLogix Inc.');
INSERT INTO dbo.filter_driver_altitudes
VALUES
('Replication', 'dvfilter.sys', 305002, 'Microsoft');
INSERT INTO dbo.filter_driver_altitudes
VALUES
('Replication', 'fsrecord.sys', 305000, 'Microsoft');
INSERT INTO dbo.filter_driver_altitudes
VALUES
('Replication', 'esff.sys', 304500, 'Beijing Cloudock Techn Co');
INSERT INTO dbo.filter_driver_altitudes
VALUES
('Replication', 'InstMon.sys', 304201, 'Numecent Inc.');
INSERT INTO dbo.filter_driver_altitudes
VALUES
('Replication', 'StreamingFSD.sys', 304200, 'Numecent Inc.');
INSERT INTO dbo.filter_driver_altitudes
VALUES
('Replication', 'ubcminifilterdriver.sys', 304100, 'Ullmore Ltd.');
INSERT INTO dbo.filter_driver_altitudes
VALUES
('Replication', 'replistor.sys', 304000, 'Legato');
INSERT INTO dbo.filter_driver_altitudes
VALUES
('Replication', 'stfsd.sys', 303900, 'Endeavors Technologies');
INSERT INTO dbo.filter_driver_altitudes
VALUES
('Replication', 'xomf.sys', 303800, 'CA (XOSOFT)');
INSERT INTO dbo.filter_driver_altitudes
VALUES
('Replication', 'nfid.sys', 303700, 'Neverfail Group Ltd');
INSERT INTO dbo.filter_driver_altitudes
VALUES
('Replication', 'sybfilter.sys', 303600, 'Sybase, Inc.');
INSERT INTO dbo.filter_driver_altitudes
VALUES
('Replication', 'rfsfilter.sys', 303500, 'Evidian');
INSERT INTO dbo.filter_driver_altitudes
VALUES
('Replication', 'cvmfsj.sys', 303400, 'CommVault Systems, Inc.');
INSERT INTO dbo.filter_driver_altitudes
VALUES
('Replication', 'iOraFilter.sys', 303300, 'Infonic plc');
INSERT INTO dbo.filter_driver_altitudes
VALUES
('Replication', 'bkbmfd32.sys (x86)', 303200, 'BakBone Software, Inc');
INSERT INTO dbo.filter_driver_altitudes
VALUES
('Replication', 'bkbmfd64.sys (x64)', 303200, 'BakBone Software, Inc');
INSERT INTO dbo.filter_driver_altitudes
VALUES
('Replication', 'mblvn.sys', 303100, 'Packeteer');
INSERT INTO dbo.filter_driver_altitudes
VALUES
('Replication', 'AV12NFNT.sys', 303000, 'AhnLab');
INSERT INTO dbo.filter_driver_altitudes
VALUES
('Replication', 'mDP_win_mini.sys', 302900, 'Macro Impact');
INSERT INTO dbo.filter_driver_altitudes
VALUES
('Replication', 'ctxubs.sys', 302800, 'Citrix Systems Inc.');
INSERT INTO dbo.filter_driver_altitudes
VALUES
('Replication', 'rrepfsf.sys', 302700, 'Rose Datasystems Inc');
INSERT INTO dbo.filter_driver_altitudes
VALUES
('Replication', 'zrbd.sys', 302110.3, 'Shanghai Fangye Network');
INSERT INTO dbo.filter_driver_altitudes
VALUES
('Replication', 'zrbdlock.sys', 302110.2, 'Shanghai Fangye Network');
INSERT INTO dbo.filter_driver_altitudes
VALUES
('Replication', 'wsyncd.sys', 302100, 'WANFast LLC');
INSERT INTO dbo.filter_driver_altitudes
VALUES
('Replication', 'cbfsfilter2017.sys', 301900, 'Super Flexible Software');
INSERT INTO dbo.filter_driver_altitudes
VALUES
('Replication', 'AxFilter.sys', 301800, 'Axcient Inc.');
INSERT INTO dbo.filter_driver_altitudes
VALUES
('Replication', 'vxfsrep.sys', 301700, 'Symantec');
INSERT INTO dbo.filter_driver_altitudes
VALUES
('Replication', 'dellcapfd.sys', 301600, 'Dell Inc.');
INSERT INTO dbo.filter_driver_altitudes
VALUES
('Replication', 'Sptres.sys', 301500, 'Safend');
INSERT INTO dbo.filter_driver_altitudes
VALUES
('Replication', 'OfficeBackup.sys', 301400, 'Ushus Technologies');
INSERT INTO dbo.filter_driver_altitudes
VALUES
('Replication', 'LxFileMirror.sys', 301350, 'Techit GmbH');
INSERT INTO dbo.filter_driver_altitudes
VALUES
('Replication', 'pcvnfilt.sys', 301300, 'Blue Coat');
INSERT INTO dbo.filter_driver_altitudes
VALUES
('Replication', 'repdac.sys', 301200, 'NSI');
INSERT INTO dbo.filter_driver_altitudes
VALUES
('Replication', 'repkap.sys', 301100, 'NSI');
INSERT INTO dbo.filter_driver_altitudes
VALUES
('Replication', 'repdrv.sys', 301000, 'NSI');
INSERT INTO dbo.filter_driver_altitudes
VALUES
('Continuous Backup', 'SyncODFA.sys', 289010, 'Sync.com Inc');
INSERT INTO dbo.filter_driver_altitudes
VALUES
('Continuous Backup', 'File_monitor.sys', 289000, 'Acronis');
INSERT INTO dbo.filter_driver_altitudes
VALUES
('Continuous Backup', 'Klcdp.sys', 288900, 'Kaspersky Lab');
INSERT INTO dbo.filter_driver_altitudes
VALUES
('Continuous Backup', 'splitinfmon.sys', 288800, 'Split Infinity');
INSERT INTO dbo.filter_driver_altitudes
VALUES
('Continuous Backup', 'versamatic.sys', 288700, 'Acertant Tech');
INSERT INTO dbo.filter_driver_altitudes
VALUES
('Continuous Backup', 'Yfilemon.sys', 288690, 'Yarisoft');
INSERT INTO dbo.filter_driver_altitudes
VALUES
('Continuous Backup', 'ibac.sys', 288600, 'Idealstor, LLC.');
INSERT INTO dbo.filter_driver_altitudes
VALUES
('Continuous Backup', 'fkdriver.sys', 288500, 'Filekeeper');
INSERT INTO dbo.filter_driver_altitudes
VALUES
('Continuous Backup', 'AAFileFilter.sys', 288300, 'Dell Inc.');
INSERT INTO dbo.filter_driver_altitudes
VALUES
('Continuous Backup', 'cbfilter20.sys', 288290.5, 'Mobile Content mgmt');
INSERT INTO dbo.filter_driver_altitudes
VALUES
('Continuous Backup', 'hyperoo.sys', 288400, 'Hyperoo Ltd');
INSERT INTO dbo.filter_driver_altitudes
VALUES
('Continuous Backup', 'HyperBacCA.sys', 285000, 'Red Gate Software Ltd');
INSERT INTO dbo.filter_driver_altitudes
VALUES
('Continuous Backup', 'ZMSFsFltr.sys', 284400, 'Zenith InfoTech');
INSERT INTO dbo.filter_driver_altitudes
VALUES
('Continuous Backup', 'AlfaSC.sys', 284300, 'Alfa Corporation');
INSERT INTO dbo.filter_driver_altitudes
VALUES
('Continuous Backup', 'hie_ifs.sys', 284200, 'Hie Electronics, Inc.');
INSERT INTO dbo.filter_driver_altitudes
VALUES
('Continuous Backup', 'AAFs.sys', 284100, 'AppAssure Software');
INSERT INTO dbo.filter_driver_altitudes
VALUES
('Continuous Backup', 'defilter.sys (old)', 284000, 'Microsoft');
INSERT INTO dbo.filter_driver_altitudes
VALUES
('Continuous Backup', 'aFsvDrv.sys', 283100, 'ITSTATION Inc');
INSERT INTO dbo.filter_driver_altitudes
VALUES
('Continuous Backup', 'tilana.sys', 283000, 'Tilana Sys');
INSERT INTO dbo.filter_driver_altitudes
VALUES
('Continuous Backup', 'VmDPFilter.sys', 282900, 'Macro Impact');
INSERT INTO dbo.filter_driver_altitudes
VALUES
('Continuous Backup', 'LbFilter.sys', 281700, 'Linkverse S.r.l.');
INSERT INTO dbo.filter_driver_altitudes
VALUES
('Continuous Backup', 'fbsfd.sys', 281600, 'Ferro Software');
INSERT INTO dbo.filter_driver_altitudes
VALUES
('Continuous Backup', 'dupleemf.sys', 281500, 'Duplee SPI, S.L.');
INSERT INTO dbo.filter_driver_altitudes
VALUES
('Continuous Backup', 'file_tracker.sys', 281420, 'Acronis Inc.');
INSERT INTO dbo.filter_driver_altitudes
VALUES
('Continuous Backup', 'exbackup.sys', 281410, 'Acronis Inc.');
INSERT INTO dbo.filter_driver_altitudes
VALUES
('Continuous Backup', 'afcdp.sys', 281400, 'Acronis Inc.');
INSERT INTO dbo.filter_driver_altitudes
VALUES
('Continuous Backup', 'dcefltr.sys', 281300, 'Cofio Software Ltd');
INSERT INTO dbo.filter_driver_altitudes
VALUES
('Continuous Backup', 'ipmrsync_mfilter.sys', 281200, 'OpenMars Enterprises');
INSERT INTO dbo.filter_driver_altitudes
VALUES
('Continuous Backup', 'cascade.sys', 281100, 'JP Software');
INSERT INTO dbo.filter_driver_altitudes
VALUES
('Continuous Backup', 'filearchive.sys', 281000, 'Code Mortem');
INSERT INTO dbo.filter_driver_altitudes
VALUES
('Continuous Backup', 'syscdp.sys', 280900, 'System OK AB');
INSERT INTO dbo.filter_driver_altitudes
VALUES
('Continuous Backup', 'dpnedriver.sys (x86)', 280850, 'HP');
INSERT INTO dbo.filter_driver_altitudes
VALUES
('Continuous Backup', 'dpnedriver64.sys (x64)', 280850, 'HP');
INSERT INTO dbo.filter_driver_altitudes
VALUES
('Continuous Backup', 'hpchgflt.sys', 280800, 'HP');
INSERT INTO dbo.filter_driver_altitudes
VALUES
('Continuous Backup', 'VirtFile.sys', 280700, 'Veritas');
INSERT INTO dbo.filter_driver_altitudes
VALUES
('Continuous Backup', 'DeqoCPS.sys', 280600, 'Deqo');
INSERT INTO dbo.filter_driver_altitudes
VALUES
('Continuous Backup', 'LV_Tracker.sys', 280500, 'LiveVault');
INSERT INTO dbo.filter_driver_altitudes
VALUES
('Continuous Backup', 'cpbak.sys', 280410, 'Checkpoint Software');
INSERT INTO dbo.filter_driver_altitudes
VALUES
('Continuous Backup', 'tdmonxp.sys', 280400, 'TimeData');
INSERT INTO dbo.filter_driver_altitudes
VALUES
('Continuous Backup', 'nvfr_cpd', 280310, 'Bakbone Software Inc.');
INSERT INTO dbo.filter_driver_altitudes
VALUES
('Continuous Backup', 'nvfr_fdd', 280300, 'Bakbone Software Inc.');
INSERT INTO dbo.filter_driver_altitudes
VALUES
('Continuous Backup', 'Sptbkp.sys', 280290, 'Safend');
INSERT INTO dbo.filter_driver_altitudes
VALUES
('Continuous Backup', 'accessmonitor.sys', 280280, 'Briljant Ekonomisystem');
INSERT INTO dbo.filter_driver_altitudes
VALUES
('Content Screener', 'anrfsdrv.sys', 268500, 'ANR Co. LTD.');
INSERT INTO dbo.filter_driver_altitudes
VALUES
('Content Screener', 'wzPrtProc.sys', 268350.5, 'ITSTATION Inc.');
INSERT INTO dbo.filter_driver_altitudes
VALUES
('Content Screener', 'taExeScanner.sys', 268350, 'ITSTATION Inc.');
INSERT INTO dbo.filter_driver_altitudes
VALUES
('Content Screener', 'GuardFSFlt.sys', 268340, 'ProShield');
INSERT INTO dbo.filter_driver_altitudes
VALUES
('Content Screener', 'usbguard.sys', 268330, 'HangZhou Tease Tech');
INSERT INTO dbo.filter_driver_altitudes
VALUES
('Content Screener', 'gibepdevflt.sys', 268320, 'Group-IB LTD');
INSERT INTO dbo.filter_driver_altitudes
VALUES
('Content Screener', 'EffeDriver.sys', 268310, 'DROVA');
INSERT INTO dbo.filter_driver_altitudes
VALUES
('Content Screener', 'Klshadow.sys', 268300, 'Kaspersky Lab');
INSERT INTO dbo.filter_driver_altitudes
VALUES
('Content Screener', 'TN28.sys', 268290, 'ID Authentication Tech');
INSERT INTO dbo.filter_driver_altitudes
VALUES
('Content Screener', 'PGDriver.sys', 268280, 'Avecto Ltd');
INSERT INTO dbo.filter_driver_altitudes
VALUES
('Content Screener', 'itseczvdb.sys', 268270, 'Innotium Inc');
INSERT INTO dbo.filter_driver_altitudes
VALUES
('Content Screener', 'unimon.sys', 268265.5, 'Unify Technologies');
INSERT INTO dbo.filter_driver_altitudes
VALUES
('Content Screener', 'isarsd.sys', 268260, 'ISARS');
INSERT INTO dbo.filter_driver_altitudes
VALUES
('Content Screener', 'zeoscanner.sys', 268255, 'PCKeeper');
INSERT INTO dbo.filter_driver_altitudes
VALUES
('Content Screener', 'fileHiders.sys', 268250, 'PCKeeper');
INSERT INTO dbo.filter_driver_altitudes
VALUES
('Content Screener', 'cbfltfs4-ObserveIT.sys', 268240, 'ObserveIT');
INSERT INTO dbo.filter_driver_altitudes
VALUES
('Content Screener', 'hipara.sys', 268230, 'Allsum LLC');
INSERT INTO dbo.filter_driver_altitudes
VALUES
('Content Screener', 'AliFileMonitorDriver.sys', 268220, 'Alibaba');
INSERT INTO dbo.filter_driver_altitudes
VALUES
('Content Screener', 'writeGuard.sys', 268210, 'TCXA Ltd.');
INSERT INTO dbo.filter_driver_altitudes
VALUES
('Content Screener', 'KKUDKProtectKer.sys', 268200, 'Goldmessage technology co., Ltd.');
INSERT INTO dbo.filter_driver_altitudes
VALUES
('Content Screener', 'HAWKFIMInt.sys', 268190, 'HAWK Network Defense');
INSERT INTO dbo.filter_driver_altitudes
VALUES
('Content Screener', 'esaccctl.sys', 268180, 'EgoSecure GmbH');
INSERT INTO dbo.filter_driver_altitudes
VALUES
('Content Screener', 'WSguard.sys', 268170, 'Wiper Software UAB');
INSERT INTO dbo.filter_driver_altitudes
VALUES
('Content Screener', 'Atomizer.sys', 268160, 'DragonFlyCodeWorks');
INSERT INTO dbo.filter_driver_altitudes
VALUES
('Content Screener', 'farwflt.sys', 268150, 'Malwarebytes');
INSERT INTO dbo.filter_driver_altitudes
VALUES
('Content Screener', 'ADSpiderEx2.sys', 268140, 'Digitalonnet');
INSERT INTO dbo.filter_driver_altitudes
VALUES
('Content Screener', 'sdfilter.sys', 268130, 'Igor Zorkov');
INSERT INTO dbo.filter_driver_altitudes
VALUES
('Content Screener', 'Safe.sys', 268120, ' ');
INSERT INTO dbo.filter_driver_altitudes
VALUES
('Content Screener', 'mydlpdelete-scanner.sys', 268110, 'Medra Teknoloji');
INSERT INTO dbo.filter_driver_altitudes
VALUES
('Content Screener', 'mydlpscanner.sys', 268100, 'Medra Teknoloji');
INSERT INTO dbo.filter_driver_altitudes
VALUES
('Content Screener', 'VrMacFlt.sys', 268080, 'Hauri Inc');
COMMIT TRAN;
GO
BEGIN TRAN;
INSERT INTO dbo.filter_driver_altitudes
VALUES
('Content Screener', 'hnpro.sys', 268040, 'Solupia');
INSERT INTO dbo.filter_driver_altitudes
VALUES
('Content Screener', 'DLDriverNetMini.sys', 268030, 'DeviceLock Inc');
INSERT INTO dbo.filter_driver_altitudes
VALUES
('Content Screener', 'ENFFLTDRV.sys', 268020, 'Enforcive Systems');
INSERT INTO dbo.filter_driver_altitudes
VALUES
('Content Screener', 'imagentpg.sys', 268012, 'Infomaximum');
INSERT INTO dbo.filter_driver_altitudes
VALUES
('Content Screener', 'crocopg.sys', 268010, 'Infomaximum');
INSERT INTO dbo.filter_driver_altitudes
VALUES
('Content Screener', 'sbapifs.sys', 268000, 'Sunbelt Software');
INSERT INTO dbo.filter_driver_altitudes
VALUES
('Content Screener', 'H6kernNT.sys', 267920, 'H6N Technologies LLC');
INSERT INTO dbo.filter_driver_altitudes
VALUES
('Content Screener', 'SGKD32.SYS', 267910, 'NetSection Security');
INSERT INTO dbo.filter_driver_altitudes
VALUES
('Content Screener', 'IccFilter.sys', 267900, 'NEC System Technologies');
INSERT INTO dbo.filter_driver_altitudes
VALUES
('Content Screener', 'tflbc.sys', 267800, 'Tani Electronics Corporation');
INSERT INTO dbo.filter_driver_altitudes
VALUES
('Content Screener', 'ArmFlt.sys', 267000, 'Armor Antivirus');
INSERT INTO dbo.filter_driver_altitudes
VALUES
('Content Screener', 'WBDrv.sys', 266700, 'Axiana LLC');
INSERT INTO dbo.filter_driver_altitudes
VALUES
('Content Screener', 'DMSamFilter.sys', 266600, 'Digimarc Corp.');
INSERT INTO dbo.filter_driver_altitudes
VALUES
('Content Screener', 'mumbl.sys', 266540, 'ZenmuTech Inc.');
INSERT INTO dbo.filter_driver_altitudes
VALUES
('Content Screener', 'DLPDriverSmb.sys', 266400.5, 'Acronis');
INSERT INTO dbo.filter_driver_altitudes
VALUES
('Content Screener', '5nine.cbt.sys', 266100, '5nine Software Inc.');
INSERT INTO dbo.filter_driver_altitudes
VALUES
('Content Screener', 'bsfs.sys', 266000, 'Quick Heal TechnologiesPvt. Ltd.');
INSERT INTO dbo.filter_driver_altitudes
VALUES
('Content Screener', 'XXRegSFilter.sys', 265910, 'Zhe Jiang Xinxin Software Tech.');
INSERT INTO dbo.filter_driver_altitudes
VALUES
('Content Screener', 'XXSFilter.sys', 265900, 'Zhe Jiang Xinxin Software Tech.');
INSERT INTO dbo.filter_driver_altitudes
VALUES
('Content Screener', 'AloahaUSBBlocker.sys', 265800, 'Wrocklage Intermedia');
INSERT INTO dbo.filter_driver_altitudes
VALUES
('Content Screener', 'frxdrv.sys', 265700, 'FSLogix Inc.');
INSERT INTO dbo.filter_driver_altitudes
VALUES
('Content Screener', 'upmAction.sys', 265650.5, 'Citrix Systems');
INSERT INTO dbo.filter_driver_altitudes
VALUES
('Content Screener', 'FolderSecure.sys', 265600, 'Max Secure Software');
INSERT INTO dbo.filter_driver_altitudes
VALUES
('Content Screener', 'XendowFLTC.sys', 265570, 'Credant Technologies');
INSERT INTO dbo.filter_driver_altitudes
VALUES
('Content Screener', 'RepDac', 265500, 'Vision Solutions');
INSERT INTO dbo.filter_driver_altitudes
VALUES
('Content Screener', 'tbbdriver.sys', 265400, 'Tedesi');
INSERT INTO dbo.filter_driver_altitudes
VALUES
('Content Screener', 'spcgrd.sys', 265300, 'FUJITSU BROAD SOLUTION');
INSERT INTO dbo.filter_driver_altitudes
VALUES
('Content Screener', 'fdtlock.sys', 265250, 'FUJITSU BROAD SOLUTION & CONSULTING Inc.');
INSERT INTO dbo.filter_driver_altitudes
VALUES
('Content Screener', 'ssfFSC.sys', 265200, 'SECUWARE S.L.');
INSERT INTO dbo.filter_driver_altitudes
VALUES
('Content Screener', 'GagSecurity.sys', 265120, 'Beijing Shu Yan Science');
INSERT INTO dbo.filter_driver_altitudes
VALUES
('Content Screener', 'PrintDriver.sys', 265110, 'Beijing Shu Yan Science');
INSERT INTO dbo.filter_driver_altitudes
VALUES
('Content Screener', 'activ.sys', 265100, 'Rapidware Pty Ltd');
INSERT INTO dbo.filter_driver_altitudes
VALUES
('Content Screener', 'avscan.sys', 265010, 'Microsoft');
INSERT INTO dbo.filter_driver_altitudes
VALUES
('Content Screener', 'scanner.sys', 265000, 'Microsoft');
INSERT INTO dbo.filter_driver_altitudes
VALUES
('Content Screener', 'DI_fs.sys', 264910, 'Soft-SB');
INSERT INTO dbo.filter_driver_altitudes
VALUES
('Content Screener', 'wgnpos.sys', 264900, 'Orchestria');
INSERT INTO dbo.filter_driver_altitudes
VALUES
('Content Screener', 'odfltr.sys', 264810, 'NetClean Technologies');
INSERT INTO dbo.filter_driver_altitudes
VALUES
('Content Screener', 'ncpafltr.sys', 264800, 'NetClean Technologies');
INSERT INTO dbo.filter_driver_altitudes
VALUES
('Content Screener', 'ct.sys', 264700, 'Haute Secure');
INSERT INTO dbo.filter_driver_altitudes
VALUES
('Content Screener', 'fvefsmf.sys', 264600, 'Fortisphere, Inc.');
INSERT INTO dbo.filter_driver_altitudes
VALUES
('Content Screener', 'block.sys', 264500, 'Autonomy Systems Limited');
INSERT INTO dbo.filter_driver_altitudes
VALUES
('Content Screener', 'csascr.sys', 264400, 'Cisco Systems');
INSERT INTO dbo.filter_driver_altitudes
VALUES
('Content Screener', 'SymAFR.sys', 264300, 'Symantec Corporation');
INSERT INTO dbo.filter_driver_altitudes
VALUES
('Content Screener', 'cwnep.sys', 264200, 'Websense Inc.');
INSERT INTO dbo.filter_driver_altitudes
VALUES
('Content Screener', 'spywareremover.sys', 264150, 'C-Netmedia');
INSERT INTO dbo.filter_driver_altitudes
VALUES
('Content Screener', 'malwarebot.sys', 264140, 'C-Netmedia');
INSERT INTO dbo.filter_driver_altitudes
VALUES
('Content Screener', 'antispywarebot.sys', 264130, '2Squared Inc.');
INSERT INTO dbo.filter_driver_altitudes
VALUES
('Content Screener', 'adwarebot.sys', 264120, 'AntiSpyware LLC');
INSERT INTO dbo.filter_driver_altitudes
VALUES
('Content Screener', 'antispyware.sys', 264110, 'AntiSpyware LLC');
INSERT INTO dbo.filter_driver_altitudes
VALUES
('Content Screener', 'spywarebot.sys', 264100, 'C-Netmedia');
INSERT INTO dbo.filter_driver_altitudes
VALUES
('Content Screener', 'nomp3.sys', 264000, 'Hamish Speirs (private developer)');
INSERT INTO dbo.filter_driver_altitudes
VALUES
('Content Screener', 'dlfilter.sys', 263900, 'Starfield Software');
INSERT INTO dbo.filter_driver_altitudes
VALUES
('Content Screener', 'sifsp.sys', 263800, 'Secure Islands Technologies LTD');
INSERT INTO dbo.filter_driver_altitudes
VALUES
('Content Screener', 'DLFsFlt.sys', 263700, 'CenterTools Software GmbH');
INSERT INTO dbo.filter_driver_altitudes
VALUES
('Content Screener', 'SamKeng.sys', 263600, 'Syvik Co, Ltd.');
INSERT INTO dbo.filter_driver_altitudes
VALUES
('Content Screener', 'rml.sys', 263500, 'Logis IT Service Gmbh');
INSERT INTO dbo.filter_driver_altitudes
VALUES
('Content Screener', 'vfsmfd.sys', 263410, 'Vontu Inc.');
INSERT INTO dbo.filter_driver_altitudes
VALUES
('Content Screener', 'vfsmfd.sys', 263400, 'Vontu Inc.');
INSERT INTO dbo.filter_driver_altitudes
VALUES
('Content Screener', 'acfilter.sys', 263300, 'Avalere, Inc.');
INSERT INTO dbo.filter_driver_altitudes
VALUES
('Content Screener', 'psecfilter.sys', 263200, 'MDI Laboratory, Inc.');
INSERT INTO dbo.filter_driver_altitudes
VALUES
('Content Screener', 'SolRedirect.sys', 263110, 'Soliton Systems');
INSERT INTO dbo.filter_driver_altitudes
VALUES
('Content Screener', 'solitkm.sys', 263100, 'Soliton Systems');
INSERT INTO dbo.filter_driver_altitudes
VALUES
('Content Screener', 'ipcfs.sys', 263000, 'NetVeda');
INSERT INTO dbo.filter_driver_altitudes
VALUES
('Content Screener', 'netgateav_access.sys', 262910, 'NETGATE Tech. s.r.o.');
INSERT INTO dbo.filter_driver_altitudes
VALUES
('Content Screener', 'spyemrg_access.sys', 262900, 'NETGATE Tech. s.r.o.');
INSERT INTO dbo.filter_driver_altitudes
VALUES
('Content Screener', 'pxrmcet.sys', 262800, 'Proxure Inc.');
INSERT INTO dbo.filter_driver_altitudes
VALUES
('Content Screener', 'EgisTecFF.sys', 262700, 'Egis Technology Inc.');
INSERT INTO dbo.filter_driver_altitudes
VALUES
('Content Screener', 'fgcpac.sys', 262600, 'Fortres Grand Corp.');
INSERT INTO dbo.filter_driver_altitudes
VALUES
('Content Screener', 'saappctl.sys', 262510, 'SecureAge Technology');
INSERT INTO dbo.filter_driver_altitudes
VALUES
('Content Screener', 'sadlp.sys', 262500, 'SecureAge Technology');
INSERT INTO dbo.filter_driver_altitudes
VALUES
('Content Screener', 'MtUsbBlockerFlt.sys', 261420.5, 'Matisoft Cyber Security');
INSERT INTO dbo.filter_driver_altitudes
VALUES
('Content Screener', 'CRExecPrev.sys', 262410, 'Cybereason');
INSERT INTO dbo.filter_driver_altitudes
VALUES
('Content Screener', 'PEG2.sys', 262400, 'PE GUARD');
INSERT INTO dbo.filter_driver_altitudes
VALUES
('Content Screener', 'AdminRunFlt.sys', 262300, 'Simon Jarvis');
INSERT INTO dbo.filter_driver_altitudes
VALUES
('Content Screener', 'wvscr.sys', 262200, 'Chengdu Wei Tech Inc.');
INSERT INTO dbo.filter_driver_altitudes
VALUES
('Content Screener', 'psepfilter.sys', 262100, 'Absolute Software');
INSERT INTO dbo.filter_driver_altitudes
VALUES
('Content Screener', 'SAMDriver.sys', 262000, 'Summit IT');
INSERT INTO dbo.filter_driver_altitudes
VALUES
('Content Screener', 'emrcore.sys', 261920, 'Ivanti Inc');
INSERT INTO dbo.filter_driver_altitudes
VALUES
('Content Screener', 'wire_fsfilter.sys', 261910, 'ThreatSpike Labs');
INSERT INTO dbo.filter_driver_altitudes
VALUES
('Content Screener', 'AMFileSystemFilter.sys', 261900, 'AppSense Ltd');
INSERT INTO dbo.filter_driver_altitudes
VALUES
('Content Screener', 'mtflt.sys', 261880, 'mTalos Inc.');
INSERT INTO dbo.filter_driver_altitudes
VALUES
('Content Screener', 'nxrmflt.sys', 261680, 'NextLabs, Inc.');
INSERT INTO dbo.filter_driver_altitudes
VALUES
('Content Screener', 'oc_fsfilter.sys', 261300, 'Raiffeisen Bank Aval');
INSERT INTO dbo.filter_driver_altitudes
VALUES
('Content Screener', 'hdlpflt.sys', 261200, 'McAfee Inc.');
INSERT INTO dbo.filter_driver_altitudes
VALUES
('Content Screener', 'CCFFilter.sys', 261160, 'Microsoft');
INSERT INTO dbo.filter_driver_altitudes
VALUES
('Content Screener', 'cbafilt.sys', 261150, 'Microsoft');
INSERT INTO dbo.filter_driver_altitudes
VALUES
('Content Screener', 'SmbBandwidthLimitFilter.sys', 261110, 'Microsoft');
INSERT INTO dbo.filter_driver_altitudes
VALUES
('Content Screener', 'DfsrRo.sys', 261100, 'Microsoft');
INSERT INTO dbo.filter_driver_altitudes
VALUES
('Content Screener', 'DataScrn.sys', 261000, 'Microsoft');
INSERT INTO dbo.filter_driver_altitudes
VALUES
('Content Screener', 'ldusbro.sys', 260900, 'LANDesk Inc.');
INSERT INTO dbo.filter_driver_altitudes
VALUES
('Content Screener', 'FileScreenFilter.sys', 260800, 'Veritas');
INSERT INTO dbo.filter_driver_altitudes
VALUES
('Content Screener', 'cpAcOnPnP.sys', 260720, 'conpal GmbH');
INSERT INTO dbo.filter_driver_altitudes
VALUES
('Content Screener', 'cpsgfsmf.sys', 260710, 'conpal GmbH');
INSERT INTO dbo.filter_driver_altitudes
VALUES
('Content Screener', 'psmmfilter.sys', 260700, 'PolyServe');
INSERT INTO dbo.filter_driver_altitudes
VALUES
('Content Screener', 'pctefa.sys', 260650, 'PC Tools Pty. Ltd.');
INSERT INTO dbo.filter_driver_altitudes
VALUES
('Content Screener', 'pctefa64.sys', 260650, 'PC Tools Pty. Ltd.');
INSERT INTO dbo.filter_driver_altitudes
VALUES
('Content Screener', 'SymEFASI64.sys', 260620, 'NortonLifeLock Inc.');
INSERT INTO dbo.filter_driver_altitudes
VALUES
('Content Screener', 'SymEFASI.sys', 260620, 'NortonLifeLock Inc.');
INSERT INTO dbo.filter_driver_altitudes
VALUES
('Content Screener', 'symefasi.sys', 260610, 'Symantec Corporation');
INSERT INTO dbo.filter_driver_altitudes
VALUES
('Content Screener', 'symefa.sys', 260600, 'Symantec');
INSERT INTO dbo.filter_driver_altitudes
VALUES
('Content Screener', 'symefa64.sys', 260600, 'Symantec');
INSERT INTO dbo.filter_driver_altitudes
VALUES
('Content Screener', 'apdFSF.sys', 260550, 'Cyberbit Ltd');
INSERT INTO dbo.filter_driver_altitudes
VALUES
('Content Screener', 'aictracedrv_cs.sys', 260500, 'AI Consulting');
INSERT INTO dbo.filter_driver_altitudes
VALUES
('Content Screener', 'DWFIxxxx.sys', 260410, 'SciencePark Corporation');
INSERT INTO dbo.filter_driver_altitudes
VALUES
('Content Screener', 'DWFIxxxx.sys', 260400, 'SciencePark Corporation');
INSERT INTO dbo.filter_driver_altitudes
VALUES
('Content Screener', 'ElasticEndpoint.sys', 260350.5, 'Elastic');
INSERT INTO dbo.filter_driver_altitudes
VALUES
('Content Screener', 'dlpflt.sys', 260340, 'Digital Endpoint');
COMMIT TRAN;
GO
BEGIN TRAN;
INSERT INTO dbo.filter_driver_altitudes
VALUES
('Content Screener', 'DSDriver.sys', 260330, 'ManageEngine Zoho Corp');
INSERT INTO dbo.filter_driver_altitudes
VALUES
('Content Screener', 'mcfltlab.sys', 260320, 'Beijing MicroColor');
INSERT INTO dbo.filter_driver_altitudes
VALUES
('Content Screener', 'FDriver.sys', 260310, 'Fox-IT');
INSERT INTO dbo.filter_driver_altitudes
VALUES
('Content Screener', 'iqpk.sys', 260300, 'Secure Islands Technologies LTD');
INSERT INTO dbo.filter_driver_altitudes
VALUES
('Content Screener', 'ZTkrnlOpRg.sys', 260264, 'Trustsoft');
INSERT INTO dbo.filter_driver_altitudes
VALUES
('Content Screener', 'ZTkrnlNt.sys', 260262, 'Trustsoft');
INSERT INTO dbo.filter_driver_altitudes
VALUES
('Content Screener', 'ZTkrnl.sys', 260260, 'Trustsoft');
INSERT INTO dbo.filter_driver_altitudes
VALUES
('Content Screener', 'VHDFlt.sys', 260240, 'Dell');
INSERT INTO dbo.filter_driver_altitudes
VALUES
('Content Screener', 'VHDFlt.sys', 260230, 'Dell');
INSERT INTO dbo.filter_driver_altitudes
VALUES
('Content Screener', 'VHDFlt.sys', 260220, 'Dell');
INSERT INTO dbo.filter_driver_altitudes
VALUES
('Content Screener', 'VHDFlt.sys', 260210, 'Dell');
INSERT INTO dbo.filter_driver_altitudes
VALUES
('Quota Management', 'dfx-qfs-fltr.sys', 245100, 'DefendX Software');
INSERT INTO dbo.filter_driver_altitudes
VALUES
('Quota Management', 'ntps_qfs.sys', 245100, 'DefendX Software');
INSERT INTO dbo.filter_driver_altitudes
VALUES
('Quota Management', 'PSSFsFilter.sys', 245000, 'PSS Systems');
INSERT INTO dbo.filter_driver_altitudes
VALUES
('Quota Management', 'Sptqmg.sys', 245300, 'Safend');
INSERT INTO dbo.filter_driver_altitudes
VALUES
('Quota Management', 'storqosflt.sys', 244000, 'Microsoft');
INSERT INTO dbo.filter_driver_altitudes
VALUES
('System Recovery', 'file_protector.sys', 227000, 'Acronis');
INSERT INTO dbo.filter_driver_altitudes
VALUES
('System Recovery', 'fbwf.sys', 226000, 'Microsoft');
INSERT INTO dbo.filter_driver_altitudes
VALUES
('System Recovery', 'BoldendDrvr.sys', 221700.5, 'Boldend, Inc.');
INSERT INTO dbo.filter_driver_altitudes
VALUES
('System Recovery', 'hmpalert.sys', 221600, 'SurfRight');
INSERT INTO dbo.filter_driver_altitudes
VALUES
('System Recovery', 'Klsysrec.sys', 221500, 'Kaspersky Lab');
INSERT INTO dbo.filter_driver_altitudes
VALUES
('System Recovery', 'SFDRV.SYS', 221400, 'Utixo LLC');
INSERT INTO dbo.filter_driver_altitudes
VALUES
('System Recovery', 'sp_prot.sys', 221300, 'Xacti Corporation');
INSERT INTO dbo.filter_driver_altitudes
VALUES
('System Recovery', 'nsfilep.sys', 221200, 'Netsupport Limited');
INSERT INTO dbo.filter_driver_altitudes
VALUES
('System Recovery', 'syscow.sys', 221100, 'System OK AB');
INSERT INTO dbo.filter_driver_altitudes
VALUES
('System Recovery', 'fsredir.sys', 221000, 'Microsoft');
INSERT INTO dbo.filter_driver_altitudes
VALUES
('Cluster File System', 'CVCBT.sys', 203400, 'CommVault Systems, Inc.');
INSERT INTO dbo.filter_driver_altitudes
VALUES
('Cluster File System', 'ResumeKeyFilter.sys', 202000, 'Microsoft');
INSERT INTO dbo.filter_driver_altitudes
VALUES
('Cluster File System', 'VeeamFCT.sys', 201900, 'Veeam Software');
INSERT INTO dbo.filter_driver_altitudes
VALUES
('Cluster File System', 'ShadowVirtualStorage.sys', 201800, 'Blade SAS');
INSERT INTO dbo.filter_driver_altitudes
VALUES
('HSM', 'wcifs.sys', 189900, 'Microsoft');
INSERT INTO dbo.filter_driver_altitudes
VALUES
('HSM', 'prjflt.sys', 189800, 'Microsoft');
INSERT INTO dbo.filter_driver_altitudes
VALUES
('HSM', 'gameflt.sys', 189750, 'Microsoft');
INSERT INTO dbo.filter_driver_altitudes
VALUES
('HSM', 'nvmsqrd.sys', 188900, 'NVIDIA Corporation');
INSERT INTO dbo.filter_driver_altitudes
VALUES
('HSM', 'Ghost_file.sys', 188800, 'Acronis');
INSERT INTO dbo.filter_driver_altitudes
VALUES
('HSM', 'RsFlt.sys', 187000, 'Redstor Limited');
INSERT INTO dbo.filter_driver_altitudes
VALUES
('HSM', 'CloudTier.sys', 186900.5, 'EaseFilter Technologies');
INSERT INTO dbo.filter_driver_altitudes
VALUES
('HSM', 'mnefs.sys', 186800, 'Nippon Techno Lab');
INSERT INTO dbo.filter_driver_altitudes
VALUES
('HSM', 'Svfsf.sys', 186700, 'Spharsoft Technologies');
INSERT INTO dbo.filter_driver_altitudes
VALUES
('HSM', 'uVaultFlt.sys', 186650, 'DOR');
INSERT INTO dbo.filter_driver_altitudes
VALUES
('HSM', 'syncmf.sys', 186620, 'Oxygen Cloud');
INSERT INTO dbo.filter_driver_altitudes
VALUES
('HSM', 'gwmemory.sys', 186600, 'Macrotec LLC');
INSERT INTO dbo.filter_driver_altitudes
VALUES
('HSM', 'cteraflt.sys', 186550, 'CTERA Networks Ltd.');
INSERT INTO dbo.filter_driver_altitudes
VALUES
('HSM', 'dbx.sys', 186500, 'Dropbox Inc.');
INSERT INTO dbo.filter_driver_altitudes
VALUES
('HSM', 'iMDrvFlt.sys', 186450, 'iManage LLC');
INSERT INTO dbo.filter_driver_altitudes
VALUES
('HSM', 'quaddrasi.sys', 186400, 'Quaddra Software');
INSERT INTO dbo.filter_driver_altitudes
VALUES
('HSM', 'gdrive.sys', 186300, 'Google');
INSERT INTO dbo.filter_driver_altitudes
VALUES
('HSM', 'CoreSyncFilter.sys', 186250, 'Adobe Systems Inc.');
INSERT INTO dbo.filter_driver_altitudes
VALUES
('HSM', 'EaseTag.sys', 186200, 'EaseVault Technologies Inc.');
INSERT INTO dbo.filter_driver_altitudes
VALUES
('HSM', 'HSFilter.sys', 186150, 'HubStor Inc.');
INSERT INTO dbo.filter_driver_altitudes
VALUES
('HSM', 'hcminifilter.sys', 186100, 'Happy Cloud Inc.');
INSERT INTO dbo.filter_driver_altitudes
VALUES
('HSM', 'PDFsFilter.sys', 186000, 'Raxco Sfotware Inc.');
INSERT INTO dbo.filter_driver_altitudes
VALUES
('HSM', 'camino.sys', 185900, 'CaminoSoft Corp');
INSERT INTO dbo.filter_driver_altitudes
VALUES
('HSM', 'C2C_AF1R.SYS', 185810, 'C2C Systems');
INSERT INTO dbo.filter_driver_altitudes
VALUES
('HSM', 'DFdriver.sys', 185800, 'DataFirst Corporation');
INSERT INTO dbo.filter_driver_altitudes
VALUES
('HSM', 'amfadrv.sys', 185700, 'Quest Software Inc.');
INSERT INTO dbo.filter_driver_altitudes
VALUES
('HSM', 'HSMdriver.sys', 185600, 'Wim Vervoorn');
INSERT INTO dbo.filter_driver_altitudes
VALUES
('HSM', 'kdfilter.sys', 185555, 'Komprise Inc.');
INSERT INTO dbo.filter_driver_altitudes
VALUES
('HSM', 'htdafd.sys', 185500, 'Bridgehead Soft');
INSERT INTO dbo.filter_driver_altitudes
VALUES
('HSM', 'odphflt.sys', 180455, 'Microsoft');
INSERT INTO dbo.filter_driver_altitudes
VALUES
('HSM', 'cldflt.sys', 180451, 'Microsoft');
INSERT INTO dbo.filter_driver_altitudes
VALUES
('HSM', 'SymHsm.sys', 185400, 'Symantec');
INSERT INTO dbo.filter_driver_altitudes
VALUES
('HSM', 'evmf.sys', 185100, 'Symantec');
INSERT INTO dbo.filter_driver_altitudes
VALUES
('HSM', 'otfilter.sys', 185000, 'Overtone Soft');
INSERT INTO dbo.filter_driver_altitudes
VALUES
('HSM', 'ithsmdrv.sys', 184900, 'IBM');
INSERT INTO dbo.filter_driver_altitudes
VALUES
('HSM', 'MfaFilter.sys', 184800, 'Waterford Technologies');
INSERT INTO dbo.filter_driver_altitudes
VALUES
('HSM', 'SonyHsmMinifilter.sys', 184700, 'Sony Corporation');
INSERT INTO dbo.filter_driver_altitudes
VALUES
('HSM', 'acahsm.sys', 184600, 'Autonomy Corporation');
INSERT INTO dbo.filter_driver_altitudes
VALUES
('HSM', 'zlhsm.sys', 184500, 'ZL Technologies');
INSERT INTO dbo.filter_driver_altitudes
VALUES
('HSM', 'CFileProtect.sys', 184100, 'Zhejiang Security Tech');
INSERT INTO dbo.filter_driver_altitudes
VALUES
('HSM', 'stc_restore_filter.sys', 184000, 'StorageCraft Technology');
INSERT INTO dbo.filter_driver_altitudes
VALUES
('HSM', 'dvfilter.sys', 183003, 'Microsoft');
INSERT INTO dbo.filter_driver_altitudes
VALUES
('HSM', 'Accesstracker.sys', 183002, 'Microsoft');
INSERT INTO dbo.filter_driver_altitudes
VALUES
('HSM', 'Changetracker.sys', 183001, 'Microsoft');
INSERT INTO dbo.filter_driver_altitudes
VALUES
('HSM', 'Fstier.sys', 183000, 'Microsoft');
INSERT INTO dbo.filter_driver_altitudes
VALUES
('HSM', 'hsmcdpflt.sys', 182700, 'Metalogix');
INSERT INTO dbo.filter_driver_altitudes
VALUES
('HSM', 'archivmgr.sys', 182690, 'Metalogix');
INSERT INTO dbo.filter_driver_altitudes
VALUES
('HSM', 'ntps_oddm.sys', 182600, 'DefendX Software');
INSERT INTO dbo.filter_driver_altitudes
VALUES
('HSM', 'XDFileSys.sys', 182500, 'XenData Limited');
INSERT INTO dbo.filter_driver_altitudes
VALUES
('HSM', 'upmjit.sys', 182400, 'Citrix Systems');
INSERT INTO dbo.filter_driver_altitudes
VALUES
('HSM', 'AtmosFS.sys', 182310, 'EMC Corporation');
INSERT INTO dbo.filter_driver_altitudes
VALUES
('HSM', 'DxSpy.sys', 182300, 'EMC Software Inc.');
INSERT INTO dbo.filter_driver_altitudes
VALUES
('HSM', 'car_hsmflt.sys', 182200, 'Caringo, Inc.');
INSERT INTO dbo.filter_driver_altitudes
VALUES
('HSM', 'BRDriver.sys', 182100, 'BitRaider');
INSERT INTO dbo.filter_driver_altitudes
VALUES
('HSM', 'BRDriver64.sys', 182100, 'BitRaider');
INSERT INTO dbo.filter_driver_altitudes
VALUES
('HSM', 'autnhsm.sys', 182000, 'Autonomy Corporation');
INSERT INTO dbo.filter_driver_altitudes
VALUES
('HSM', 'cthsmflt.sys', 181970, 'ComTrade');
INSERT INTO dbo.filter_driver_altitudes
VALUES
('HSM', 'NxMini.sys', 181900, 'NEXSAN');
INSERT INTO dbo.filter_driver_altitudes
VALUES
('HSM', 'neuflt.sys', 181818, 'NeuShield');
INSERT INTO dbo.filter_driver_altitudes
VALUES
('HSM', 'npfdaflt.sys', 181800, 'Mimosa Systems Inc');
INSERT INTO dbo.filter_driver_altitudes
VALUES
('HSM', 'AppStream.sys', 181700, 'AppStream, Inc.');
INSERT INTO dbo.filter_driver_altitudes
VALUES
('HSM', 'HPEDpHsmX64.sys', 181620, 'Hewlett-Packard, Co.');
INSERT INTO dbo.filter_driver_altitudes
VALUES
('HSM', 'HPArcHsmX64.sys', 181610, 'Hewlett-Packard, Co.');
INSERT INTO dbo.filter_driver_altitudes
VALUES
('HSM', 'hphsmflt.sys', 181600, 'Hewlett-Packard, Co.');
INSERT INTO dbo.filter_driver_altitudes
VALUES
('HSM', 'cparchsm.sys', 181610, 'Micro Focus');
INSERT INTO dbo.filter_driver_altitudes
VALUES
('HSM', 'RepHsm.sys', 181500, 'Double-Take Software, Inc.');
INSERT INTO dbo.filter_driver_altitudes
VALUES
('HSM', 'RepSIS.sys', 181490, 'Double-Take Software');
INSERT INTO dbo.filter_driver_altitudes
VALUES
('HSM', 'SquashCompressionFsFilter.sys', 181410, 'Squash Compression');
INSERT INTO dbo.filter_driver_altitudes
VALUES
('HSM', 'GXHSM.sys', 181400, 'Commvault Systems, Inc');
INSERT INTO dbo.filter_driver_altitudes
VALUES
('HSM', 'EdsiHsm.sys', 181300, 'Enterprise Data Solutions, Inc.');
INSERT INTO dbo.filter_driver_altitudes
VALUES
('HSM', 'BkfMap.sys', 181200, 'Data Storage Group');
INSERT INTO dbo.filter_driver_altitudes
VALUES
('HSM', 'hsmfilter.sys', 181100, 'GRAU Data Storage AG');
INSERT INTO dbo.filter_driver_altitudes
VALUES
('HSM', 'mwilcflt.sys', 181020, 'Moonwalk Universal P/L');
INSERT INTO dbo.filter_driver_altitudes
VALUES
('HSM', 'mwildflt.sys', 181015, 'Moonwalk Universal P/L');
INSERT INTO dbo.filter_driver_altitudes
VALUES
('HSM', 'mwilsflt.sys', 181010, 'Moonwalk Universal P/L');
INSERT INTO dbo.filter_driver_altitudes
VALUES
('HSM', 'mwidmflt.sys', 181000, 'Moonwalk Universal P/L');
INSERT INTO dbo.filter_driver_altitudes
VALUES
('HSM', 'HcpAwfs.sys', 181960, 'Hitachi Data Systems');
INSERT INTO dbo.filter_driver_altitudes
VALUES
('HSM', 'sdrefltr.sys', 180950, 'Hitachi Data Systems');
COMMIT TRAN;
GO
BEGIN TRAN;
INSERT INTO dbo.filter_driver_altitudes
VALUES
('HSM', 'fltasm.sys', 180900, 'Global 360');
INSERT INTO dbo.filter_driver_altitudes
VALUES
('HSM', 'cnet_hsm.sys', 180850, 'Carroll-Net Inc.');
INSERT INTO dbo.filter_driver_altitudes
VALUES
('HSM', 'pntvolflt.sys', 180800, 'PoINT Software&Systems');
INSERT INTO dbo.filter_driver_altitudes
VALUES
('HSM', 'appxstrm.sys', 180710, 'Microsoft');
INSERT INTO dbo.filter_driver_altitudes
VALUES
('HSM', 'wimmount.sys', 180700, 'Microsoft');
INSERT INTO dbo.filter_driver_altitudes
VALUES
('HSM', 'hsmflt.sys', 180600, 'Microsoft');
INSERT INTO dbo.filter_driver_altitudes
VALUES
('HSM', 'dfsrflt.sys', 180500, 'Microsoft');
INSERT INTO dbo.filter_driver_altitudes
VALUES
('HSM', 'StorageSyncGuard.sys', 180465, 'Microsoft');
INSERT INTO dbo.filter_driver_altitudes
VALUES
('HSM', 'StorageSync.sys', 180460, 'Microsoft');
INSERT INTO dbo.filter_driver_altitudes
VALUES
('HSM', 'dedup.sys', 180450, 'Microsoft');
INSERT INTO dbo.filter_driver_altitudes
VALUES
('HSM', 'dfmflt.sys', 180410, 'Microsoft');
INSERT INTO dbo.filter_driver_altitudes
VALUES
('HSM', 'sis.sys', 180400, 'Microsoft');
INSERT INTO dbo.filter_driver_altitudes
VALUES
('HSM', 'rbt_wfd.sys', 180300, 'Riverbed Technology,Inc');
INSERT INTO dbo.filter_driver_altitudes
VALUES
('Imaging (ex: .ZIP)', 'pfmfs_???.sys', 172100, 'Pismo Technic Inc');
INSERT INTO dbo.filter_driver_altitudes
VALUES
('Imaging (ex: .ZIP)', 'virtual_file.sys', 172000, 'Acronis');
INSERT INTO dbo.filter_driver_altitudes
VALUES
('Imaging (ex: .ZIP)', 'wimFltr.sys', 170500, 'Microsoft');
INSERT INTO dbo.filter_driver_altitudes
VALUES
('Compression', 'CmgFFC.sys', 166000, 'Credant Technologies');
INSERT INTO dbo.filter_driver_altitudes
VALUES
('Compression', 'compress.sys', 165000, 'Microsoft');
INSERT INTO dbo.filter_driver_altitudes
VALUES
('Compression', 'cmpflt.sys', 162000, 'Microsoft');
INSERT INTO dbo.filter_driver_altitudes
VALUES
('Compression', 'IridiumIO.sys', 161700, 'Confio');
INSERT INTO dbo.filter_driver_altitudes
VALUES
('Compression', 'zzenc.sys', 161650.5, 'Imdtech LLC');
INSERT INTO dbo.filter_driver_altitudes
VALUES
('Compression', 'logcompressor.sys', 161600, 'VelociSQL Inc.');
INSERT INTO dbo.filter_driver_altitudes
VALUES
('Compression', 'GcfFilter.sys', 161500, 'GemacmbH');
INSERT INTO dbo.filter_driver_altitudes
VALUES
('Compression', 'ssddoubler.sys', 161400, 'Sinan Karaca');
INSERT INTO dbo.filter_driver_altitudes
VALUES
('Compression', 'Sptcmp.sys', 161300, 'Safend');
INSERT INTO dbo.filter_driver_altitudes
VALUES
('Compression', 'wimfsf.sys', 161000, 'Microsoft');
INSERT INTO dbo.filter_driver_altitudes
VALUES
('Compression', 'GEFCMP.sys', 160100, 'Symantec');
INSERT INTO dbo.filter_driver_altitudes
VALUES
('Encryption', 'AAFS.sys', 149110, 'ViGero');
INSERT INTO dbo.filter_driver_altitudes
VALUES
('Encryption', 'FJSeparettiFilterRamMon.sys', 149100, 'FUJITSU LIMITED');
INSERT INTO dbo.filter_driver_altitudes
VALUES
('Encryption', 'trsxefs.sys', 149060, 'TransientX Inc.');
INSERT INTO dbo.filter_driver_altitudes
VALUES
('Encryption', 'psatfilter.sys', 149050, 'ProYuga');
INSERT INTO dbo.filter_driver_altitudes
VALUES
('Encryption', 'RdFilter.sys', 149040, 'CyberEye Research Labs');
INSERT INTO dbo.filter_driver_altitudes
VALUES
('Encryption', 'gisfile_decryption.sys', 149030, 'Communication U China');
INSERT INTO dbo.filter_driver_altitudes
VALUES
('Encryption', 'TIFSFilter.sys', 149020, 'SG Corporation');
INSERT INTO dbo.filter_driver_altitudes
VALUES
('Encryption', 'OsrDt2.sys', 149010, 'Information Security Corp');
INSERT INTO dbo.filter_driver_altitudes
VALUES
('Encryption', 'EasyKryptMF.sys', 149000, 'SoftKrypt LLC');
INSERT INTO dbo.filter_driver_altitudes
VALUES
('Encryption', 'padlock.sys', 148910, 'IntSoft Inc.');
INSERT INTO dbo.filter_driver_altitudes
VALUES
('Encryption', 'ffecore.sys', 148900, 'Winmagic');
INSERT INTO dbo.filter_driver_altitudes
VALUES
('Encryption', 'bkfs.sys', 148880, 'Hangzhou JoyBlock Ltd');
INSERT INTO dbo.filter_driver_altitudes
VALUES
('Encryption', 'fangcloud.sys', 148860, 'Hangzhou Yifangyun');
INSERT INTO dbo.filter_driver_altitudes
VALUES
('Encryption', 'FileGuard.sys', 148820.5, 'EaseFilter Technologies');
INSERT INTO dbo.filter_driver_altitudes
VALUES
('Encryption', 'klvfs.sys', 148810, 'Kaspersky Lab');
INSERT INTO dbo.filter_driver_altitudes
VALUES
('Encryption', 'Klfle.sys', 148800, 'Kaspersky Lab');
INSERT INTO dbo.filter_driver_altitudes
VALUES
('Encryption', 'ISFP.sys', 148701, 'ALPS SYSTEM INTEGRATIO');
INSERT INTO dbo.filter_driver_altitudes
VALUES
('Encryption', 'ISIRM.sys', 148700, 'ALPS SYSTEM INTERGRATION CO., LTD');
INSERT INTO dbo.filter_driver_altitudes
VALUES
('Encryption', 'fhfs.sys', 148670.5, 'SecureCircle');
INSERT INTO dbo.filter_driver_altitudes
VALUES
('Encryption', 'ASUSSecDrive.sys', 148650, 'ASUS');
INSERT INTO dbo.filter_driver_altitudes
VALUES
('Encryption', 'ABFilterDriver.sys', 148640, 'AlertBoot');
INSERT INTO dbo.filter_driver_altitudes
VALUES
('Encryption', 'QDocumentFSF.sys', 148630, 'BicDroid Inc.');
INSERT INTO dbo.filter_driver_altitudes
VALUES
('Encryption', 'bfusbenc.sys', 148620, 'bitFence Inc.');
INSERT INTO dbo.filter_driver_altitudes
VALUES
('Encryption', 'sztgbfsf.sys', 148610, 'SaferZone Co.');
INSERT INTO dbo.filter_driver_altitudes
VALUES
('Encryption', 'mwIPSDFilter.sys', 148600, 'Egis Technology Inc.');
INSERT INTO dbo.filter_driver_altitudes
VALUES
('Encryption', 'csccvdrv.sys', 148500, 'Computer Sciences Corporation');
INSERT INTO dbo.filter_driver_altitudes
VALUES
('Encryption', 'aefs.sys', 148400, 'Angelltech Corporation Xi''an');
INSERT INTO dbo.filter_driver_altitudes
VALUES
('Encryption', 'VTEFsFlt.sys', 148374, 'EsComputer Corp');
INSERT INTO dbo.filter_driver_altitudes
VALUES
('Encryption', 'IWCSEFlt.sys', 148300, 'InfoWatch');
INSERT INTO dbo.filter_driver_altitudes
VALUES
('Encryption', 'GDDmk.sys', 148250, 'G Data Software AG');
INSERT INTO dbo.filter_driver_altitudes
VALUES
('Encryption', 'clcxcore.sys', 148210, 'AFORE Solutions Inc.');
INSERT INTO dbo.filter_driver_altitudes
VALUES
('Encryption', 'OrisLPDrv.sys', 148200, 'CGS Publishing Tech');
INSERT INTO dbo.filter_driver_altitudes
VALUES
('Encryption', 'nlemsys.sys', 148100, 'NETLIB');
INSERT INTO dbo.filter_driver_altitudes
VALUES
('Encryption', 'prvflder.sys', 148000, 'Microsoft');
INSERT INTO dbo.filter_driver_altitudes
VALUES
('Encryption', 'ssefs.sys', 147900, 'SecuLution GmbH');
INSERT INTO dbo.filter_driver_altitudes
VALUES
('Encryption', 'SePSed.sys', 147800, 'Humming Heads, Inc.');
INSERT INTO dbo.filter_driver_altitudes
VALUES
('Encryption', 'dlmfencx.sys', 147700, 'Data Encryption Ltd');
INSERT INTO dbo.filter_driver_altitudes
VALUES
('Encryption', 'SkyDEnc.sys', 147620, 'Sky Co Ltd');
INSERT INTO dbo.filter_driver_altitudes
VALUES
('Encryption', 'psgcrypt.sys', 147610, 'Yokogawa R&L Corp');
INSERT INTO dbo.filter_driver_altitudes
VALUES
('Encryption', 'bbfsflt.sys', 147600, 'Bloombase');
INSERT INTO dbo.filter_driver_altitudes
VALUES
('Encryption', 'qx10efs.sys', 147500, 'Quixxant');
INSERT INTO dbo.filter_driver_altitudes
VALUES
('Encryption', 'MEfefs.sys', 147400, 'Eruces Inc.');
INSERT INTO dbo.filter_driver_altitudes
VALUES
('Encryption', 'medlpflt.sys', 147310, 'Check Point Software Technologies Ltd');
INSERT INTO dbo.filter_driver_altitudes
VALUES
('Encryption', 'dsfa.sys', 147308, 'Check Point Software Technologies Ltd');
INSERT INTO dbo.filter_driver_altitudes
VALUES
('Encryption', 'Snicrpt.sys', 147300, 'Systemneeds, Inc');
INSERT INTO dbo.filter_driver_altitudes
VALUES
('Encryption', 'iCrypt.sys', 147200, 'I-O DATA DEVICE, INC.');
INSERT INTO dbo.filter_driver_altitudes
VALUES
('Encryption', 'xdrmflt.sys', 147100, 'bluefinsystems');
INSERT INTO dbo.filter_driver_altitudes
VALUES
('Encryption', 'dyFsFilter.sys', 147000, 'Scrypto Media');
INSERT INTO dbo.filter_driver_altitudes
VALUES
('Encryption', 'thinairwin.sys', 146960, 'Thin Air Inc"');
INSERT INTO dbo.filter_driver_altitudes
VALUES
('Encryption', 'UcaDataMgr.sys', 146950, 'AppSense Ltd');
INSERT INTO dbo.filter_driver_altitudes
VALUES
('Encryption', 'zesocc.sys', 146900, 'Novell');
INSERT INTO dbo.filter_driver_altitudes
VALUES
('Encryption', 'mfprom.sys', 146800, 'McAfee Inc');
INSERT INTO dbo.filter_driver_altitudes
VALUES
('Encryption', 'MfeEEFF.sys', 146790, 'McAfee Inc.');
INSERT INTO dbo.filter_driver_altitudes
VALUES
('Encryption', 'intefs.sys', 146700, 'TianYu Software');
INSERT INTO dbo.filter_driver_altitudes
VALUES
('Encryption', 'leofs.sys', 146600, 'Leotech');
INSERT INTO dbo.filter_driver_altitudes
VALUES
('Encryption', 'autocryptater.sys', 146500, 'Richard Hagen');
INSERT INTO dbo.filter_driver_altitudes
VALUES
('Encryption', 'WavxDMgr.sys', 146400, 'Scott Cochrane');
INSERT INTO dbo.filter_driver_altitudes
VALUES
('Encryption', 'eedmkxp32.sys', 146300, 'Entrust');
INSERT INTO dbo.filter_driver_altitudes
VALUES
('Encryption', 'SbCe.sys', 146200, 'SafeBoot');
INSERT INTO dbo.filter_driver_altitudes
VALUES
('Encryption', 'iSharedFsFilter', 146100, 'Packeteer Inc');
INSERT INTO dbo.filter_driver_altitudes
VALUES
('Encryption', 'dlrmenc.sys', 146010, 'DESlock');
INSERT INTO dbo.filter_driver_altitudes
VALUES
('Encryption', 'dlmfenc.sys', 146000, 'DESlock');
INSERT INTO dbo.filter_driver_altitudes
VALUES
('Encryption', 'aksdf.sys', 145900, 'Aladdin Knowledge Systems');
INSERT INTO dbo.filter_driver_altitudes
VALUES
('Encryption', 'DDSFilter.sys', 145800, 'WuHan Forworld Software');
INSERT INTO dbo.filter_driver_altitudes
VALUES
('Encryption', 'SecureShield.sys', 145700, 'HMI');
INSERT INTO dbo.filter_driver_altitudes
VALUES
('Encryption', 'AifaFE.sys', 145600, 'Alfa');
INSERT INTO dbo.filter_driver_altitudes
VALUES
('Encryption', 'HiCrypt.sys', 145566, 'digitronic computersysteme GmbH');
INSERT INTO dbo.filter_driver_altitudes
VALUES
('Encryption', 'GBFsMf.sys', 145500, 'GreenBorder');
INSERT INTO dbo.filter_driver_altitudes
VALUES
('Encryption', 'jmefs.sys', 145400, 'ShangHai Elec');
INSERT INTO dbo.filter_driver_altitudes
VALUES
('Encryption', 'emugufs.sys', 145333, 'Emugu Secure FS');
INSERT INTO dbo.filter_driver_altitudes
VALUES
('Encryption', 'VFDriver.sys', 145300, 'R Systems');
INSERT INTO dbo.filter_driver_altitudes
VALUES
('Encryption', 'IntelDG.sys', 145250, 'Intel Corporation');
INSERT INTO dbo.filter_driver_altitudes
VALUES
('Encryption', 'DPMEncrypt.sys', 145240, 'Randtronics Pty');
INSERT INTO dbo.filter_driver_altitudes
VALUES
('Encryption', 'EVSDecrypt64.sys', 145230, 'Fortium Technologies Ltd');
INSERT INTO dbo.filter_driver_altitudes
VALUES
('Encryption', 'skycryptorencfs.sys', 145220, 'Onecryptor CJSC.');
INSERT INTO dbo.filter_driver_altitudes
VALUES
('Encryption', 'AisLeg.sys', 145210, 'Assured Information Security');
INSERT INTO dbo.filter_driver_altitudes
VALUES
('Encryption', 'windtalk.sys', 145200, 'Hyland Software');
INSERT INTO dbo.filter_driver_altitudes
VALUES
('Encryption', 'TeamCryptor.sys', 145190, 'iTwin Pte. Ltd.');
INSERT INTO dbo.filter_driver_altitudes
VALUES
('Encryption', 'CVDLP.sys', 145180, 'CommVault Systems, Inc.');
INSERT INTO dbo.filter_driver_altitudes
VALUES
('Encryption', '5nine.encryptor.sys', 145170, '5nine Software Inc.');
INSERT INTO dbo.filter_driver_altitudes
VALUES
('Encryption', 'ctpfile.sys', 145160, 'Beijing Wondersoft Technology Co., Ltd');
INSERT INTO dbo.filter_driver_altitudes
VALUES
('Encryption', 'DPDrv.sys', 145150, 'IBM Japan, Ltd.');
INSERT INTO dbo.filter_driver_altitudes
VALUES
('Encryption', 'tsdlp.sys', 145140, 'Forware');
INSERT INTO dbo.filter_driver_altitudes
VALUES
('Encryption', 'KCDriver.sys', 145130, 'Tallegra Ltd');
INSERT INTO dbo.filter_driver_altitudes
VALUES
('Encryption', 'CmgFFE.sys', 145120, 'Credant Technologies');
INSERT INTO dbo.filter_driver_altitudes
VALUES
('Encryption', 'fgcenc.sys', 145110, 'Fortres Grand Corp.');
INSERT INTO dbo.filter_driver_altitudes
VALUES
('Encryption', 'sview.sys', 145100, 'Cinea');
COMMIT TRAN;
GO
BEGIN TRAN;
INSERT INTO dbo.filter_driver_altitudes
VALUES
('Encryption', 'TalkeyFilterDriver.sys', 145040, 'myTALKEY s.r.o.');
INSERT INTO dbo.filter_driver_altitudes
VALUES
('Encryption', 'MtUsbFlt19.sys', 145020.5, 'Matisoft Cyber Security');
INSERT INTO dbo.filter_driver_altitudes
VALUES
('Encryption', 'FedsFilterDriver.sys', 145010, 'Physical Optics Corp');
INSERT INTO dbo.filter_driver_altitudes
VALUES
('Encryption', 'stocc.sys', 145000, 'Senforce Technologies');
INSERT INTO dbo.filter_driver_altitudes
VALUES
('Encryption', 'SnEfs.sys', 144900, 'Informzaschita');
INSERT INTO dbo.filter_driver_altitudes
VALUES
('Encryption', 'ewSecureDox', 144800, 'Echoworx Corporation');
INSERT INTO dbo.filter_driver_altitudes
VALUES
('Encryption', 'osrdmk.sys', 144700, 'OSR Open Systems Resources, Inc.');
INSERT INTO dbo.filter_driver_altitudes
VALUES
('Encryption', 'uldcr.sys', 144600, 'NCR Financial Solutions');
INSERT INTO dbo.filter_driver_altitudes
VALUES
('Encryption', 'Tkefsxp.sys - 32bit', 144500, 'INCA Internet Co., Ltd');
INSERT INTO dbo.filter_driver_altitudes
VALUES
('Encryption', 'Tkefsxp64.sys - 64bit', 144500, 'INCA Internet Co., Ltd');
INSERT INTO dbo.filter_driver_altitudes
VALUES
('Encryption', 'NmlAccf.sys', 144400, 'NEC System Technologies, Ltd.');
INSERT INTO dbo.filter_driver_altitudes
VALUES
('Encryption', 'SolCrypt.sys', 144300, 'Soliton Systems K.K.');
INSERT INTO dbo.filter_driver_altitudes
VALUES
('Encryption', 'IngDmk.sys', 144200, 'Ingrian Networks, Inc.');
INSERT INTO dbo.filter_driver_altitudes
VALUES
('Encryption', 'llenc.sys', 144100, 'SecureAxis Software');
INSERT INTO dbo.filter_driver_altitudes
VALUES
('Encryption', 'SecureData.sys', 144030, 'SecureAge Technology');
INSERT INTO dbo.filter_driver_altitudes
VALUES
('Encryption', 'lockcube.sys', 144020, 'SecureAge Technology Pte Ltd');
INSERT INTO dbo.filter_driver_altitudes
VALUES
('Encryption', 'sdmedia.sys', 144010, 'SecureAge Technology');
INSERT INTO dbo.filter_driver_altitudes
VALUES
('Encryption', 'mysdrive.sys', 144000, 'SecureAge Technology');
INSERT INTO dbo.filter_driver_altitudes
VALUES
('Encryption', 'FileArmor.sys', 143900, 'Mobile Armor');
INSERT INTO dbo.filter_driver_altitudes
VALUES
('Encryption', 'VSTXEncr.sys', 143800, 'VIA Technologies, Inc.');
INSERT INTO dbo.filter_driver_altitudes
VALUES
('Encryption', 'dgdmk.sys', 143700, 'Verdasys Inc.');
INSERT INTO dbo.filter_driver_altitudes
VALUES
('Encryption', 'shandy.sys', 143600, 'Safend Ltd.');
INSERT INTO dbo.filter_driver_altitudes
VALUES
('Encryption', 'C2knet.sys', 143520, 'Secuware');
INSERT INTO dbo.filter_driver_altitudes
VALUES
('Encryption', 'C2kdef.sys', 143510, 'Secuware');
INSERT INTO dbo.filter_driver_altitudes
VALUES
('Encryption', 'ssfFS.sys', 143500, 'SECUWARE S.L.');
INSERT INTO dbo.filter_driver_altitudes
VALUES
('Encryption', 'PISRFE.sys', 143400, 'Jilin University IT Co.');
INSERT INTO dbo.filter_driver_altitudes
VALUES
('Encryption', 'bapfecre.sys', 143300, 'BitArmor Systems, Inc');
INSERT INTO dbo.filter_driver_altitudes
VALUES
('Encryption', 'KPSD.sys', 143200, 'cihosoft');
INSERT INTO dbo.filter_driver_altitudes
VALUES
('Encryption', 'Fcfileio.sys', 143100, 'Brainzsquare, Co. Ltd.');
INSERT INTO dbo.filter_driver_altitudes
VALUES
('Encryption', 'cpdrm.sys', 143000, 'Pikewerks');
INSERT INTO dbo.filter_driver_altitudes
VALUES
('Encryption', 'vmfiltr.sys', 142900, 'Vormetric Inc');
INSERT INTO dbo.filter_driver_altitudes
VALUES
('Encryption', 'Sfntpffd.sys', 142890, 'Thales CPL');
INSERT INTO dbo.filter_driver_altitudes
VALUES
('Encryption', 'VFSEnc.sys', 142811, 'Symantec');
INSERT INTO dbo.filter_driver_altitudes
VALUES
('Encryption', 'pgpfs.sys', 142810, 'Symantec');
INSERT INTO dbo.filter_driver_altitudes
VALUES
('Encryption', 'fencry.sys', 142800, 'Symantec');
INSERT INTO dbo.filter_driver_altitudes
VALUES
('Encryption', 'TmFileEncDmk.sys', 142700, 'Trend Micro Inc');
INSERT INTO dbo.filter_driver_altitudes
VALUES
('Encryption', 'cpefs.sys', 142600, 'Crypto-Pro');
INSERT INTO dbo.filter_driver_altitudes
VALUES
('Encryption', 'dekfs.sys', 142500, 'KasherLab co.,ltd');
INSERT INTO dbo.filter_driver_altitudes
VALUES
('Encryption', 'qlockfilter.sys', 142400, 'Binqsoft Inc.');
INSERT INTO dbo.filter_driver_altitudes
VALUES
('Encryption', 'RRFilterDriverStack_d3.sys', 142300, 'Rational Retention');
INSERT INTO dbo.filter_driver_altitudes
VALUES
('Encryption', 'cve.sys', 142200, 'Absolute Software Corp.');
INSERT INTO dbo.filter_driver_altitudes
VALUES
('Encryption', 'spcflt.sys', 142100, 'FUJITSU BSC Inc.');
INSERT INTO dbo.filter_driver_altitudes
VALUES
('Encryption', 'ldsecusb.sys', 142000, 'LANDesk Inc.');
INSERT INTO dbo.filter_driver_altitudes
VALUES
('Encryption', 'fencr.sys', 141900, 'SODATSW spol. s.r.o.');
INSERT INTO dbo.filter_driver_altitudes
VALUES
('Encryption', 'bw_fssec.sys', 141850.5, 'Wuhan Buwei Software Technology Co.,Ltd');
INSERT INTO dbo.filter_driver_altitudes
VALUES
('Encryption', 'RubiFlt.sys', 141800, 'Hitachi, Ltd.');
INSERT INTO dbo.filter_driver_altitudes
VALUES
('Encryption', 'NCrypt.sys', 141700, 'Nimshi Corp');
INSERT INTO dbo.filter_driver_altitudes
VALUES
('Encryption', 'pske.sys', 141661, 'Penta Security Systems');
INSERT INTO dbo.filter_driver_altitudes
VALUES
('Encryption', 'mfild.sys', 141660, 'Penta Security Systems');
INSERT INTO dbo.filter_driver_altitudes
VALUES
('Encryption', 'cbfsfilter2017.sys', 141635, 'Automaton Inc');
INSERT INTO dbo.filter_driver_altitudes
VALUES
('Encryption', 'cbfsfilter2017.sys', 141634, 'Automaton Inc');
INSERT INTO dbo.filter_driver_altitudes
VALUES
('Encryption', 'cbfsfilter2017.sys', 141633, 'Automaton Inc');
INSERT INTO dbo.filter_driver_altitudes
VALUES
('Encryption', 'cbfsfilter2017.sys', 141632, 'Automaton Inc');
INSERT INTO dbo.filter_driver_altitudes
VALUES
('Encryption', 'cbfsfilter2017.sys', 141631, 'Automaton Inc');
INSERT INTO dbo.filter_driver_altitudes
VALUES
('Encryption', 'cbfsfilter2017.sys', 141630, 'Automaton Inc');
INSERT INTO dbo.filter_driver_altitudes
VALUES
('Encryption', 'TypeSquare.sys', 141620, 'Morisawa inc.');
INSERT INTO dbo.filter_driver_altitudes
VALUES
('Encryption', 'xbdocfilter.sys', 141610, 'Zrxb');
INSERT INTO dbo.filter_driver_altitudes
VALUES
('Encryption', 'EVSDecrypt32.sys', 141600, 'Fortium Technologies Ltd');
INSERT INTO dbo.filter_driver_altitudes
VALUES
('Encryption', 'EVSDecrypt64.sys', 141600, 'Fortium Technologies Ltd');
INSERT INTO dbo.filter_driver_altitudes
VALUES
('Encryption', 'EVSDecryptia64.sys', 141600, 'Fortium Technologies Ltd');
INSERT INTO dbo.filter_driver_altitudes
VALUES
('Encryption', 'SophosDt2.sys', 141510, 'Sophos Plc');
INSERT INTO dbo.filter_driver_altitudes
VALUES
('Encryption', 'afdriver.sys', 141500, 'ATUS Technology LLC');
INSERT INTO dbo.filter_driver_altitudes
VALUES
('Encryption', 'TrivalentFSFltr.sys', 141430, 'Cyber Reliant');
INSERT INTO dbo.filter_driver_altitudes
VALUES
('Encryption', 'CmdMnEfs.sys', 141420, 'Comodo Security');
INSERT INTO dbo.filter_driver_altitudes
VALUES
('Encryption', 'DWENxxxx.sys', 141410, 'SciencePark Corporation');
INSERT INTO dbo.filter_driver_altitudes
VALUES
('Encryption', 'DWENxxxx.sys', 141400, 'SciencePark Corporation');
INSERT INTO dbo.filter_driver_altitudes
VALUES
('Encryption', 'westlight.sys', 141350, 'Westlight AI');
INSERT INTO dbo.filter_driver_altitudes
VALUES
('Encryption', 'hdFileSentryDrv32.sys', 141300, 'Heilig Defense');
INSERT INTO dbo.filter_driver_altitudes
VALUES
('Encryption', 'hdFileSentryDrv64.sys', 141300, 'Heilig Defense');
INSERT INTO dbo.filter_driver_altitudes
VALUES
('Encryption', 'SDSCloudDrv.sys', 141255, 'Stormshield');
INSERT INTO dbo.filter_driver_altitudes
VALUES
('Encryption', 'pnpfs.sys', 141250, 'PNP SECURE INC');
INSERT INTO dbo.filter_driver_altitudes
VALUES
('Encryption', 'SmartCipherFilter.sys', 141240, 'Micro Focus');
INSERT INTO dbo.filter_driver_altitudes
VALUES
('Encryption', 'cplcdt2.sys', 141230, 'conpal GmbH');
INSERT INTO dbo.filter_driver_altitudes
VALUES
('Encryption', 'asCryptoFilter.sys', 141220, 'Applied Security GmbH');
INSERT INTO dbo.filter_driver_altitudes
VALUES
('Encryption', 'NetCryptKR.sys', 141210, 'NetCrypt Pty Ltd');
INSERT INTO dbo.filter_driver_altitudes
VALUES
('Encryption', 'SGFS.sys', 141205, 'Levyco Development,LLC');
INSERT INTO dbo.filter_driver_altitudes
VALUES
('Encryption', 'BHFilter.sys', 141200, 'Beachhead Solutions');
INSERT INTO dbo.filter_driver_altitudes
VALUES
('Encryption', 'Filecrypt.sys', 141100, 'Microsoft');
INSERT INTO dbo.filter_driver_altitudes
VALUES
('Encryption', 'encrypt.sys', 141010, 'Microsoft');
INSERT INTO dbo.filter_driver_altitudes
VALUES
('Encryption', 'swapBuffers.sys', 141000, 'Microsoft');
INSERT INTO dbo.filter_driver_altitudes
VALUES
('Virtualization', 'Klvirt.sys', 138100, 'Kaspersky Lab');
INSERT INTO dbo.filter_driver_altitudes
VALUES
('Virtualization', 'eseadriver3z.sys', 138080, 'ESEA');
INSERT INTO dbo.filter_driver_altitudes
VALUES
('Virtualization', 'thsmmf.sys', 138060, 'Talon Storage Solutions');
INSERT INTO dbo.filter_driver_altitudes
VALUES
('Virtualization', 'VMagic.sys', 138050, 'AI Consulting');
COMMIT TRAN;
GO
BEGIN TRAN;
INSERT INTO dbo.filter_driver_altitudes
VALUES
('Virtualization', 'GetSAS.sys', 138040, 'SAS Institute Inc');
INSERT INTO dbo.filter_driver_altitudes
VALUES
('Virtualization', 'rqtNos.sys', 138030, 'ReaQta Ltd.');
INSERT INTO dbo.filter_driver_altitudes
VALUES
('Virtualization', 'HIPS64.sys', 138020, 'Recrypt LLC');
INSERT INTO dbo.filter_driver_altitudes
VALUES
('Virtualization', 'frxdrv.sys', 138010, 'FSLogix Inc.');
INSERT INTO dbo.filter_driver_altitudes
VALUES
('Virtualization', 'vzdrv.sys', 138000, 'Altiris');
INSERT INTO dbo.filter_driver_altitudes
VALUES
('Virtualization', 'sffsg.sys', 137990, 'Starfish Storage Corp');
INSERT INTO dbo.filter_driver_altitudes
VALUES
('Virtualization', 'AppStream.sys', 137920, 'Symantec Corporation');
INSERT INTO dbo.filter_driver_altitudes
VALUES
('Virtualization', 'Rasm.sys', 137915, 'OpDesk Inc');
INSERT INTO dbo.filter_driver_altitudes
VALUES
('Virtualization', 'boxifier.sys', 137910, 'Kenubi');
INSERT INTO dbo.filter_driver_altitudes
VALUES
('Virtualization', 'xorw.sys', 137900, 'CA (XOsoft)');
INSERT INTO dbo.filter_driver_altitudes
VALUES
('Virtualization', 'ctlua.sys', 137800, 'SurfRight B.V.');
INSERT INTO dbo.filter_driver_altitudes
VALUES
('Virtualization', 'fgccow.sys', 137700, 'Fortres Grand Corp.');
INSERT INTO dbo.filter_driver_altitudes
VALUES
('Virtualization', 'aswSnx.sys', 137600, 'ALWIL Software');
INSERT INTO dbo.filter_driver_altitudes
VALUES
('Virtualization', 'AppIsoFltr.sys', 137500, 'Kernel Drivers');
INSERT INTO dbo.filter_driver_altitudes
VALUES
('Virtualization', 'ptcvfsd.sys', 137400, 'PTC');
INSERT INTO dbo.filter_driver_altitudes
VALUES
('Virtualization', 'CloudFile.sys', 137350.5, 'EaseFilter Technologies');
INSERT INTO dbo.filter_driver_altitudes
VALUES
('Virtualization', 'BDSandBox.sys', 137300, 'BitDefender SRL');
INSERT INTO dbo.filter_driver_altitudes
VALUES
('Virtualization', 'sxfpss-virt.sys', 137200, 'Skanix AS');
INSERT INTO dbo.filter_driver_altitudes
VALUES
('Virtualization', 'DKRtWrt.sys', 137100, 'Diskeeper Corporation');
INSERT INTO dbo.filter_driver_altitudes
VALUES
('Virtualization', 'ivm.sys', 137000, 'RingCube Technologies');
INSERT INTO dbo.filter_driver_altitudes
VALUES
('Virtualization', 'ivm.sys', 136990, 'Citrix Systems');
INSERT INTO dbo.filter_driver_altitudes
VALUES
('Virtualization', 'dtiof.sys', 136900, 'Instavia Software Inc.');
INSERT INTO dbo.filter_driver_altitudes
VALUES
('Virtualization', 'NxTopCP.sys', 136800, 'Virtual Ccomputer Inc.');
INSERT INTO dbo.filter_driver_altitudes
VALUES
('Virtualization', 'svdriver.sys', 136700, 'VMware, Inc.');
INSERT INTO dbo.filter_driver_altitudes
VALUES
('Virtualization', 'PPnP-LocalBoost2.sys', 136650.5, 'Edgeless Opensource Group');
INSERT INTO dbo.filter_driver_altitudes
VALUES
('Virtualization', 'unifltr.sys', 136600, 'Unidesk');
INSERT INTO dbo.filter_driver_altitudes
VALUES
('Virtualization', 'unidrive.sys (Renamed)', 136600, 'Unidesk');
INSERT INTO dbo.filter_driver_altitudes
VALUES
('Virtualization', 'unirsd.sys', 136600, 'Unidesk');
INSERT INTO dbo.filter_driver_altitudes
VALUES
('Virtualization', 'ive.sys', 136500, 'TrendMicro Inc.');
INSERT INTO dbo.filter_driver_altitudes
VALUES
('Virtualization', 'odamf.sys', 136450, 'Sony Corporation');
INSERT INTO dbo.filter_driver_altitudes
VALUES
('Virtualization', 'SrMxfMf.sys', 136440, 'Sony Corporation');
INSERT INTO dbo.filter_driver_altitudes
VALUES
('Virtualization', 'pszmf.sys', 136430, 'Sony Corporation');
INSERT INTO dbo.filter_driver_altitudes
VALUES
('Virtualization', 'sxsudfmf.sys', 136410, 'Sony Corporation');
INSERT INTO dbo.filter_driver_altitudes
VALUES
('Virtualization', 'vfammf.sys', 136400, 'Sony Corporation');
INSERT INTO dbo.filter_driver_altitudes
VALUES
('Virtualization', 'lwfsflt.sys', 136300, 'Liquidware Labs');
INSERT INTO dbo.filter_driver_altitudes
VALUES
('Virtualization', 'VHDFlt.sys', 136240, 'Dell');
INSERT INTO dbo.filter_driver_altitudes
VALUES
('Virtualization', 'VHDFlt.sys', 136230, 'Dell');
INSERT INTO dbo.filter_driver_altitudes
VALUES
('Virtualization', 'VHDFlt.sys', 136220, 'Dell');
INSERT INTO dbo.filter_driver_altitudes
VALUES
('Virtualization', 'VHDFlt.sys', 136210, 'Dell');
INSERT INTO dbo.filter_driver_altitudes
VALUES
('Virtualization', 'ncfsfltr.sys', 136200, 'NComputing Inc.');
INSERT INTO dbo.filter_driver_altitudes
VALUES
('Virtualization', 'cmdguard.sys', 136100, 'COMODO Security Solutions Inc');
INSERT INTO dbo.filter_driver_altitudes
VALUES
('Virtualization', 'hpfsredir.sys', 136000, 'HP');
INSERT INTO dbo.filter_driver_altitudes
VALUES
('Virtualization', 'svhdxflt.sys', 135100, 'Microsoft');
INSERT INTO dbo.filter_driver_altitudes
VALUES
('Virtualization', 'luafv.sys', 135000, 'Microsoft');
INSERT INTO dbo.filter_driver_altitudes
VALUES
('Virtualization', 'ivm.sys', 134000, 'RingCube Technologies');
INSERT INTO dbo.filter_driver_altitudes
VALUES
('Virtualization', 'ivm.sys', 133990, 'Citrix Systems');
INSERT INTO dbo.filter_driver_altitudes
VALUES
('Virtualization', 'RevBitsEPSMF.sys', 132730.5, 'RevBits LLC');
INSERT INTO dbo.filter_driver_altitudes
VALUES
('Virtualization', 'RasRdpFs.sys', 132720, 'Parallels International');
INSERT INTO dbo.filter_driver_altitudes
VALUES
('Virtualization', 'frxdrvvt.sys', 132700, 'FSLogix Inc.');
INSERT INTO dbo.filter_driver_altitudes
VALUES
('Virtualization', 'pfmfs_???.sys', 132600, 'Pismo Technic Inc.');
INSERT INTO dbo.filter_driver_altitudes
VALUES
('Virtualization', 'Stcvhdmf.sys', 132600, 'StorageCraft Tech Corp');
INSERT INTO dbo.filter_driver_altitudes
VALUES
('Virtualization', 'appdrv01.sys', 132500, 'Protection Technology');
INSERT INTO dbo.filter_driver_altitudes
VALUES
('Virtualization', 'virtual_file.sys', 132400, 'Acronis');
INSERT INTO dbo.filter_driver_altitudes
VALUES
('Virtualization', 'pdiFsFilter.sys', 132300, 'Proximal Data Inc.');
INSERT INTO dbo.filter_driver_altitudes
VALUES
('Virtualization', 'avgvtx86.sys', 132200, 'AVG Technologies CZ, s.r.o.');
INSERT INTO dbo.filter_driver_altitudes
VALUES
('Virtualization', 'avgvtx64.sys', 132200, 'AVG Technologies CZ, s.r.o.');
INSERT INTO dbo.filter_driver_altitudes
VALUES
('Virtualization', 'DataNet_Driver.sys', 132100, 'AppSense Ltd');
INSERT INTO dbo.filter_driver_altitudes
VALUES
('Virtualization', 'EgenPage.sys', 132000, 'Egenera, Inc.');
INSERT INTO dbo.filter_driver_altitudes
VALUES
('Virtualization', 'unidrive.sys-old', 131900, 'Unidesk');
INSERT INTO dbo.filter_driver_altitudes
VALUES
('Virtualization', 'ivm.sys.old', 131800, 'RingCube Technologies');
INSERT INTO dbo.filter_driver_altitudes
VALUES
('Virtualization', 'XiaobaiFsR.sys', 131710, 'SHENZHEN UNNOO LTD');
INSERT INTO dbo.filter_driver_altitudes
VALUES
('Virtualization', 'XiaobaiFs.sys', 131700, 'SHENZHEN UNNOO LTD');
INSERT INTO dbo.filter_driver_altitudes
VALUES
('Virtualization', 'iotfsflt.sys', 131600, 'IO Turbine Inc');
INSERT INTO dbo.filter_driver_altitudes
VALUES
('Virtualization', 'mhpvfs.sys', 131500, 'Wunix Limited');
INSERT INTO dbo.filter_driver_altitudes
VALUES
('Virtualization', 'svdriver.sys', 131400, 'SnapVolumes Inc.');
INSERT INTO dbo.filter_driver_altitudes
VALUES
('Virtualization', 'Sptvrt.sys', 131300, 'Safend');
INSERT INTO dbo.filter_driver_altitudes
VALUES
('Virtualization', 'antirswf.sys', 131210, 'Panzor Cybersecurity');
INSERT INTO dbo.filter_driver_altitudes
VALUES
('Virtualization', 'aicvirt.sys', 131200, 'AI Consulting');
INSERT INTO dbo.filter_driver_altitudes
VALUES
('Virtualization', 'MEMEPMAgent.sys', 130852, 'Microsoft');
INSERT INTO dbo.filter_driver_altitudes
VALUES
('Virtualization', 'sfo.sys', 130100, 'Microsoft');
INSERT INTO dbo.filter_driver_altitudes
VALUES
('Virtualization', 'DeVolume.sys', 130000, 'Microsoft');
INSERT INTO dbo.filter_driver_altitudes
VALUES
('Physical Quota management', 'quota.sys', 125000, 'Microsoft');
INSERT INTO dbo.filter_driver_altitudes
VALUES
('Physical Quota management', 'qafilter.sys', 124000, 'Veritas');
INSERT INTO dbo.filter_driver_altitudes
VALUES
('Physical Quota management', 'DroboFlt.sys', 123900, 'Data Robotics');
INSERT INTO dbo.filter_driver_altitudes
VALUES
('Open File', 'insyncmf.sys', 105000, 'InSync');
INSERT INTO dbo.filter_driver_altitudes
VALUES
('Open File', 'cbfilter20.sys', 101010, 'Bentley Systems Inc');
INSERT INTO dbo.filter_driver_altitudes
VALUES
('Open File', 'SPILock8.sys', 100900, 'Software Pursuits Inc.');
INSERT INTO dbo.filter_driver_altitudes
VALUES
('Open File', 'Klbackupflt.sys', 100800, 'Kaspersky');
INSERT INTO dbo.filter_driver_altitudes
VALUES
('Open File', 'repkap', 100700, 'Vision Solutions');
INSERT INTO dbo.filter_driver_altitudes
VALUES
('Open File', 'symrg.sys', 100600, 'Symantec');
INSERT INTO dbo.filter_driver_altitudes
VALUES
('Open File', 'adsfilter.sys', 100500, 'PolyServ');
INSERT INTO dbo.filter_driver_altitudes
VALUES
('Open File', 'FMonitor.sys', 100490, 'Safetica');
INSERT INTO dbo.filter_driver_altitudes
VALUES
('Security Enhancer', 'KpHrd.sys', 88300, 'Ivanti');
INSERT INTO dbo.filter_driver_altitudes
VALUES
('Security Enhancer', 'cbfilter20.sys', 88250, 'Division-M');
INSERT INTO dbo.filter_driver_altitudes
VALUES
('Security Enhancer', 'flflt.sys', 88240.5, 'PNP SECURE INC');
INSERT INTO dbo.filter_driver_altitudes
VALUES
('Security Enhancer', 'pfcflt.sys', 88240, 'PNP SECURE INC');
INSERT INTO dbo.filter_driver_altitudes
VALUES
('Security Enhancer', 'arm_minifilter.sys', 88232, 'Assured Info Security');
INSERT INTO dbo.filter_driver_altitudes
VALUES
('Security Enhancer', 'pegasus.sys', 88230, 'Assured Info Security');
INSERT INTO dbo.filter_driver_altitudes
VALUES
('Security Enhancer', 'RSBDrv.sys', 88220, 'SMTechnology Co.');
INSERT INTO dbo.filter_driver_altitudes
VALUES
('Security Enhancer', 'psprotf.sys', 88210, 'Panzor Cybersecurity');
INSERT INTO dbo.filter_driver_altitudes
VALUES
('Security Enhancer', 'DPMACL.sys', 88100, 'Randtronics Pty');
INSERT INTO dbo.filter_driver_altitudes
VALUES
('Security Enhancer', 'dsbwnck.sys', 88000, 'Easy Solution Inc.');
INSERT INTO dbo.filter_driver_altitudes
VALUES
('Security Enhancer', 'cbfilter20.sys', 87911, 'Automaton Inc');
INSERT INTO dbo.filter_driver_altitudes
VALUES
('Security Enhancer', 'cbfsfilter2017.sys', 87910, 'Automaton Inc');
INSERT INTO dbo.filter_driver_altitudes
VALUES
('Security Enhancer', 'cbfsfilter2017.sys', 87909, 'Automaton Inc');
INSERT INTO dbo.filter_driver_altitudes
VALUES
('Security Enhancer', 'cbfsfilter2017.sys', 87908, 'Automaton Inc');
INSERT INTO dbo.filter_driver_altitudes
VALUES
('Security Enhancer', 'cbfsfilter2017.sys', 87907, 'Automaton Inc');
INSERT INTO dbo.filter_driver_altitudes
VALUES
('Security Enhancer', 'cbfsfilter2017.sys', 87906, 'Automaton Inc');
INSERT INTO dbo.filter_driver_altitudes
VALUES
('Security Enhancer', 'cbfsfilter2017.sys', 87905, 'Automaton Inc');
INSERT INTO dbo.filter_driver_altitudes
VALUES
('Security Enhancer', 'cbfsfilter2017.sys', 87904, 'Automaton Inc');
INSERT INTO dbo.filter_driver_altitudes
VALUES
('Security Enhancer', 'cbfsfilter2017.sys', 87903, 'Automaton Inc');
INSERT INTO dbo.filter_driver_altitudes
VALUES
('Security Enhancer', 'cbfsfilter2017.sys', 87902, 'Automaton Inc');
INSERT INTO dbo.filter_driver_altitudes
VALUES
('Security Enhancer', 'cbfsfilter2017.sys', 87901, 'Automaton Inc');
INSERT INTO dbo.filter_driver_altitudes
VALUES
('Security Enhancer', 'cbfsfilter2017.sys', 87900, 'Automaton Inc');
INSERT INTO dbo.filter_driver_altitudes
VALUES
('Security Enhancer', 'RansomStopDriver.sys', 87810, 'Maddrix LLC');
INSERT INTO dbo.filter_driver_altitudes
VALUES
('Security Enhancer', 'rsbfsfilter.sys', 87800, 'Corel Corporation');
INSERT INTO dbo.filter_driver_altitudes
VALUES
('Security Enhancer', 'hsmltflt.sys', 87720, 'Hitachi Solutions');
INSERT INTO dbo.filter_driver_altitudes
VALUES
('Security Enhancer', 'hssfflt.sys', 87710, 'Hitachi Solutions');
INSERT INTO dbo.filter_driver_altitudes
VALUES
('Security Enhancer', 'acmnflt.sys', 87708, 'Hitachi Solutions');
INSERT INTO dbo.filter_driver_altitudes
VALUES
('Security Enhancer', 'ACSKFFD.sys', 87700, 'Hitachi Solutions');
INSERT INTO dbo.filter_driver_altitudes
VALUES
('Security Enhancer', 'MyDLPMF.sys', 87600, 'Comodo Group Inc.');
INSERT INTO dbo.filter_driver_altitudes
VALUES
('Security Enhancer', 'asioeg.sys', 87550.5, 'Encourage Technologies');
INSERT INTO dbo.filter_driver_altitudes
VALUES
('Security Enhancer', 'ScuaRaw.sys', 87500, 'SCUA Segurança da Informação');
INSERT INTO dbo.filter_driver_altitudes
VALUES
('Security Enhancer', 'HDSFilter.sys', 87400, 'NeoAutus Automation System');
INSERT INTO dbo.filter_driver_altitudes
VALUES
('Security Enhancer', 'ikfsmflt.sys', 87300, 'IronKey Inc.');
INSERT INTO dbo.filter_driver_altitudes
VALUES
('Security Enhancer', 'Klsec.sys', 87200, 'Kaspersky Lab');
INSERT INTO dbo.filter_driver_altitudes
VALUES
('Security Enhancer', 'XtimUSBFsFilterDrv.sys', 87190, 'Dalian CP-SDT Ltd');
INSERT INTO dbo.filter_driver_altitudes
VALUES
('Security Enhancer', 'RGFLT_FM.sys', 87180, 'Hauri.inc');
INSERT INTO dbo.filter_driver_altitudes
VALUES
('Security Enhancer', 'flockflt.sys', 87170, 'Ahranta Inc.');
INSERT INTO dbo.filter_driver_altitudes
VALUES
('Security Enhancer', 'ZdCore.sys', 87160, 'Zends Technological Solutions');
INSERT INTO dbo.filter_driver_altitudes
VALUES
('Security Enhancer', 'dcrypt.sys', 87150, 'ReactOS Foundation');
INSERT INTO dbo.filter_driver_altitudes
VALUES
('Security Enhancer', 'hpradeo.sys', 87140, 'Pradeo');
INSERT INTO dbo.filter_driver_altitudes
VALUES
('Security Enhancer', 'SDFSAGDRV.SYS', 87130, 'ALPS SYSTEM INTERGRATION CO., LTD.');
INSERT INTO dbo.filter_driver_altitudes
VALUES
('Security Enhancer', 'SDFSDEVFDRV.SYS', 87120, 'ALPS SYSTEM INTERGRATION CO., LTD.');
INSERT INTO dbo.filter_driver_altitudes
VALUES
('Security Enhancer', 'SDIFSFDRV.SYS', 87110, 'ALPS SYSTEM INTERGRATION CO., LTD.');
INSERT INTO dbo.filter_driver_altitudes
VALUES
('Security Enhancer', 'SDFSFDRV.SYS', 87100, 'ALPS SYSTEM INTERGRATION CO., LTD.');
INSERT INTO dbo.filter_driver_altitudes
VALUES
('Security Enhancer', 'CModule.sys', 87070, 'Zhejiang Security Tech');
INSERT INTO dbo.filter_driver_altitudes
VALUES
('Security Enhancer', 'HHRRPH.sys', 87010, 'H+H Software GmbH');
INSERT INTO dbo.filter_driver_altitudes
VALUES
('Security Enhancer', 'HHVolFltr.sys', 87000, 'H+H Software GmbH');
INSERT INTO dbo.filter_driver_altitudes
VALUES
('Security Enhancer', 'CCRRSecMon.sys', 86960, 'Cyber Crucible Inc.');
INSERT INTO dbo.filter_driver_altitudes
VALUES
('Security Enhancer', 'RevoNetDriver.sys', 86940.5, 'J''s Communication Co.');
INSERT INTO dbo.filter_driver_altitudes
VALUES
('Security Enhancer', 'SbieDrv.sys', 86900, 'Sandboxie L.T.D');
INSERT INTO dbo.filter_driver_altitudes
VALUES
('Security Enhancer', 'assetpro.sys', 86800, 'pyaprotect․com');
INSERT INTO dbo.filter_driver_altitudes
VALUES
('Security Enhancer', 'dlp.sys', 86700, 'Tellus Software AS');
INSERT INTO dbo.filter_driver_altitudes
VALUES
('Security Enhancer', 'eps.sys', 86600, 'Lumension Security');
INSERT INTO dbo.filter_driver_altitudes
VALUES
('Security Enhancer', 'RapportPG64.sys', 86500, 'Trusteer');
INSERT INTO dbo.filter_driver_altitudes
VALUES
('Security Enhancer', 'amminifilter.sys', 86400, 'AppSense');
INSERT INTO dbo.filter_driver_altitudes
VALUES
('Security Enhancer', 'Sniflt.sys', 86300, 'Systemneeds, Inc');
INSERT INTO dbo.filter_driver_altitudes
VALUES
('Security Enhancer', 'SecFile.sys', 86200, 'Secure By Design Inc.');
INSERT INTO dbo.filter_driver_altitudes
VALUES
('Security Enhancer', 'philly.sys', 86110, 'triCerat Inc.');
INSERT INTO dbo.filter_driver_altitudes
VALUES
('Security Enhancer', 'reggy.sys', 86100, 'triCerat Inc.');
INSERT INTO dbo.filter_driver_altitudes
VALUES
('Security Enhancer', 'cygfilt.sys', 86000, 'Livegrid Incorporated');
INSERT INTO dbo.filter_driver_altitudes
VALUES
('Security Enhancer', 'prelaunch.sys', 85900, 'D3L');
INSERT INTO dbo.filter_driver_altitudes
VALUES
('Security Enhancer', 'csareg.sys', 85810, 'Cisco Systems');
INSERT INTO dbo.filter_driver_altitudes
VALUES
('Security Enhancer', 'csaenh.sys', 85800, 'Cisco Systems');
INSERT INTO dbo.filter_driver_altitudes
VALUES
('Security Enhancer', 'asi_ns_drv.sys', 85750.5, 'ASHINI Co. Ltd.');
INSERT INTO dbo.filter_driver_altitudes
VALUES
('Security Enhancer', 'asEpsDrv.sys', 85750, 'ASHINI Co. Ltd.');
INSERT INTO dbo.filter_driver_altitudes
VALUES
('Security Enhancer', 'CIDACL.sys', 85700, 'GE Aviation (Digital Systems Germantown)');
INSERT INTO dbo.filter_driver_altitudes
VALUES
('Security Enhancer', 'CVDLP.sys', 85610, 'CommVault Systems, Inc.');
INSERT INTO dbo.filter_driver_altitudes
VALUES
('Security Enhancer', 'QGPEFlt.sys', 85600, 'Quest Software');
INSERT INTO dbo.filter_driver_altitudes
VALUES
('Security Enhancer', 'Drveng.sys', 85500, 'CA');
INSERT INTO dbo.filter_driver_altitudes
VALUES
('Security Enhancer', 'PPDMFilter_x64.sys', 85550.5, 'PolicyPak Software Inc');
INSERT INTO dbo.filter_driver_altitudes
VALUES
('Security Enhancer', 'PPDMFilter_x86.sys', 85550.5, 'PolicyPak Software Inc');
INSERT INTO dbo.filter_driver_altitudes
VALUES
('Security Enhancer', 'vracfil2.sys', 85400, 'HAURI');
INSERT INTO dbo.filter_driver_altitudes
VALUES
('Security Enhancer', 'TFsDisk.sys', 85300, 'Teruten');
INSERT INTO dbo.filter_driver_altitudes
VALUES
('Security Enhancer', 'rcMiniDrv.sys', 85200, 'REDGATE CO.,LTD.');
INSERT INTO dbo.filter_driver_altitudes
VALUES
('Security Enhancer', 'SnMc5xx.sys', 85100, 'Informzaschita');
INSERT INTO dbo.filter_driver_altitudes
VALUES
('Security Enhancer', 'FSPFltd.sys', 85010, 'Alfa');
INSERT INTO dbo.filter_driver_altitudes
VALUES
('Security Enhancer', 'AifaFFP.sys', 85000, 'Alfa');
INSERT INTO dbo.filter_driver_altitudes
VALUES
('Security Enhancer', 'EsAccCtlFE.sys', 84901, 'EgoSecure GmbH');
INSERT INTO dbo.filter_driver_altitudes
VALUES
('Security Enhancer', 'DpAccCtl.sys', 84900, 'Softbroker GmbH');
INSERT INTO dbo.filter_driver_altitudes
VALUES
('Security Enhancer', 'privman.sys', 84800, 'BeyondTrust');
INSERT INTO dbo.filter_driver_altitudes
VALUES
('Security Enhancer', 'eumntvol.sys', 84700, 'Eugrid Inc');
INSERT INTO dbo.filter_driver_altitudes
VALUES
('Security Enhancer', 'SoloEncFilter.sys', 84600, 'Soliton Systems');
INSERT INTO dbo.filter_driver_altitudes
VALUES
('Security Enhancer', 'sbfilter.sys', 84500, 'UC4 Sofware');
INSERT INTO dbo.filter_driver_altitudes
VALUES
('Security Enhancer', 'cposfw.sys', 84450, 'Check Point Software Technologies Ltd');
INSERT INTO dbo.filter_driver_altitudes
VALUES
('Security Enhancer', 'vsdatant.sys', 84400, 'Zone Labs LLC');
INSERT INTO dbo.filter_driver_altitudes
VALUES
('Security Enhancer', 'SePnet.sys', 84350, 'Humming Heads, Inc.');
INSERT INTO dbo.filter_driver_altitudes
VALUES
('Security Enhancer', 'SePuld.sys', 84340, 'Humming Heads, Inc.');
INSERT INTO dbo.filter_driver_altitudes
VALUES
('Security Enhancer', 'SePpld.sys', 84330, 'Humming Heads, Inc.');
INSERT INTO dbo.filter_driver_altitudes
VALUES
('Security Enhancer', 'SePfsd.sys', 84320, 'Humming Heads, Inc.');
INSERT INTO dbo.filter_driver_altitudes
VALUES
('Security Enhancer', 'SePwld.sys', 84310, 'Humming Heads, Inc.');
INSERT INTO dbo.filter_driver_altitudes
VALUES
('Security Enhancer', 'SePprd.sys', 84300, 'Humming Heads, Inc.');
INSERT INTO dbo.filter_driver_altitudes
VALUES
('Security Enhancer', 'InPFlter.sys', 84200, 'Humming Heads, Inc.');
INSERT INTO dbo.filter_driver_altitudes
VALUES
('Security Enhancer', 'CProCtrl.sys', 84100, 'Crypto-Pro');
INSERT INTO dbo.filter_driver_altitudes
VALUES
('Security Enhancer', 'spyshelter.sys', 84000, 'Datpol');
INSERT INTO dbo.filter_driver_altitudes
VALUES
('Security Enhancer', 'clpinspprot.sys', 83900, 'Information Technology Company Ltd.');
INSERT INTO dbo.filter_driver_altitudes
VALUES
('Security Enhancer', 'AbrEpm.sys', 83800, 'FastTrack Software ApS');
INSERT INTO dbo.filter_driver_altitudes
VALUES
('Security Enhancer', 'uvmfsflt.sys', 83376, 'NEC Corporation');
INSERT INTO dbo.filter_driver_altitudes
VALUES
('Security Enhancer', 'ProtectIt.sys', 82373, 'TeraByte Inc.');
INSERT INTO dbo.filter_driver_altitudes
VALUES
('Security Enhancer', 'dguard.sys', 82300, 'Dmitry Varshavsky');
INSERT INTO dbo.filter_driver_altitudes
VALUES
('Security Enhancer', 'NSUSBStorageFilter.sys', 82200, 'NetSupport Ltd');
INSERT INTO dbo.filter_driver_altitudes
VALUES
('Security Enhancer', 'RMSEFFMV.SYS', 82100, 'CJSC Returnil Software');
INSERT INTO dbo.filter_driver_altitudes
VALUES
('Security Enhancer', 'BoksFLAC.sys', 82000, 'Fox Technologies');
INSERT INTO dbo.filter_driver_altitudes
VALUES
('Security Enhancer', 'cpAcOnPnP.sys', 81910, 'conpal GmbH');
INSERT INTO dbo.filter_driver_altitudes
VALUES
('Security Enhancer', 'cpsgfsmf.sys', 81900, 'conpal GmbH');
INSERT INTO dbo.filter_driver_altitudes
VALUES
('Security Enhancer', 'ndevsec.sys', 81800, 'Norman ASA');
INSERT INTO dbo.filter_driver_altitudes
VALUES
('Security Enhancer', 'ViewIntus_RTDG.sys', 81700, 'Pentego Technologies Ltd');
INSERT INTO dbo.filter_driver_altitudes
VALUES
('Security Enhancer', 'BKSandFS.sys', 81640, 'Binklac Workstation');
INSERT INTO dbo.filter_driver_altitudes
VALUES
('Security Enhancer', 'BWAnticheat.sys', 81638, 'Binklac Workstation');
INSERT INTO dbo.filter_driver_altitudes
VALUES
('Security Enhancer', 'airlock.sys', 81630, 'Airlock Digital Pty Ltd');
INSERT INTO dbo.filter_driver_altitudes
VALUES
('Security Enhancer', 'zam.sys', 81620, ' ');
INSERT INTO dbo.filter_driver_altitudes
VALUES
('Security Enhancer', 'ANXfsm.sys', 81610, 'Arcdo');
INSERT INTO dbo.filter_driver_altitudes
VALUES
('Security Enhancer', 'CDrSDFlt.sys', 81600, 'Arcdo');
INSERT INTO dbo.filter_driver_altitudes
VALUES
('Security Enhancer', 'crnselfdefence32.sys', 81500, 'Coranti Inc.');
INSERT INTO dbo.filter_driver_altitudes
VALUES
('Security Enhancer', 'crnselfdefence64.sys', 81500, 'Coranti Inc.');
INSERT INTO dbo.filter_driver_altitudes
VALUES
('Security Enhancer', 'zlock_drv.sys', 81400, 'SecurIT');
INSERT INTO dbo.filter_driver_altitudes
VALUES
('Security Enhancer', 'f101fs.sys', 81300, 'Fortres Grand Corp.');
INSERT INTO dbo.filter_driver_altitudes
VALUES
('Security Enhancer', 'sysgar.sys', 81200, 'Nucleus Data Recover');
INSERT INTO dbo.filter_driver_altitudes
VALUES
('Security Enhancer', 'EmbargoM.sys', 81100, 'ScriptLogic');
INSERT INTO dbo.filter_driver_altitudes
VALUES
('Security Enhancer', 'KSkyMonitor.sys', 81080, 'Sky Monitor');
INSERT INTO dbo.filter_driver_altitudes
VALUES
('Security Enhancer', 'ngssdef.sys', 81050, 'Wontok Inc');
INSERT INTO dbo.filter_driver_altitudes
VALUES
('Security Enhancer', 'ssb.sys', 81041, 'Wontok Inc');
INSERT INTO dbo.filter_driver_altitudes
VALUES
('Security Enhancer', 'regflt.sys', 81040, 'Wontok Inc');
INSERT INTO dbo.filter_driver_altitudes
VALUES
('Security Enhancer', 'fsds2a.sys', 81000, 'Splitstreem Ltd');
INSERT INTO dbo.filter_driver_altitudes
VALUES
('Security Enhancer', 'HeimdalInsights.sys', 80950.5, 'Heimdal Security A/S');
INSERT INTO dbo.filter_driver_altitudes
VALUES
('Security Enhancer', 'csacentr.sys', 80900, 'Cisco Systems');
INSERT INTO dbo.filter_driver_altitudes
VALUES
('Security Enhancer', 'ScvFLT50.sys', 80850, 'Secuve Ltd');
INSERT INTO dbo.filter_driver_altitudes
VALUES
('Security Enhancer', 'paritydriver.sys', 80800, 'Bit9, Inc.');
INSERT INTO dbo.filter_driver_altitudes
VALUES
('Security Enhancer', 'nkfsprot.sys', 80710, 'Konneka');
INSERT INTO dbo.filter_driver_altitudes
VALUES
('Security Enhancer', 'nkprot.sys', 80700, 'KONNEKA Information Technologies');
INSERT INTO dbo.filter_driver_altitudes
VALUES
('Security Enhancer', 'acpadlock.sys', 80691, 'IntSoft Co., Ltd');
INSERT INTO dbo.filter_driver_altitudes
VALUES
('Security Enhancer', 'ksmf.sys', 80690, 'IntSoft Co., Ltd');
INSERT INTO dbo.filter_driver_altitudes
VALUES
('Security Enhancer', 'amsdk.sys', 80682.5, 'WatchDogDevelopment.com, LLC');
INSERT INTO dbo.filter_driver_altitudes
VALUES
('Security Enhancer', 'im.sys', 80680, 'CrowdStrike');
INSERT INTO dbo.filter_driver_altitudes
VALUES
('Security Enhancer', 'SophosED.sys', 80670, 'Sophos');
INSERT INTO dbo.filter_driver_altitudes
VALUES
('Security Enhancer', 'jazzfile.sys', 80660, 'Jazz Networks');
INSERT INTO dbo.filter_driver_altitudes
VALUES
('Security Enhancer', 'SMXFs.sys', 80500, 'OSR');
INSERT INTO dbo.filter_driver_altitudes
VALUES
('Copy Protection', 'plypFsMon.sys', 67100, 'PolyPort Inc.');
INSERT INTO dbo.filter_driver_altitudes
VALUES
('Copy Protection', 'd3clock.sys', 67000, 'D3CRYPT3D LLC');
INSERT INTO dbo.filter_driver_altitudes
VALUES
('Copy Protection', 'cbfltfs4.sys', 66500, 'I3D Technology Inc');
INSERT INTO dbo.filter_driver_altitudes
VALUES
('Copy Protection', 'CkProcess.sys', 66100, 'KASHU SYSTEM DESIGN INC.');
INSERT INTO dbo.filter_driver_altitudes
VALUES
('Copy Protection', 'dlmfprot.sys', 66000, 'Data Encrypt Sys');
INSERT INTO dbo.filter_driver_altitudes
VALUES
('Copy Protection', 'baprtsef.sys', 65700, 'BitArmor Systems, Inc');
INSERT INTO dbo.filter_driver_altitudes
VALUES
('Copy Protection', 'sxfpss.sys', 65600, 'Skanix AS');
INSERT INTO dbo.filter_driver_altitudes
VALUES
('Copy Protection', 'rgasdev.sys', 65500, 'Macrovision');
INSERT INTO dbo.filter_driver_altitudes
VALUES
('Copy Protection', 'SkyFPDrv.sys', 65410, 'Sky Co. Ltd.');
INSERT INTO dbo.filter_driver_altitudes
VALUES
('Copy Protection', 'SkyLWP.sys', 65400, 'Sky Co. Ltd.');
INSERT INTO dbo.filter_driver_altitudes
VALUES
('Copy Protection', 'SkySDVRF.sys', 65390, 'Sky Co. Ltd.');
INSERT INTO dbo.filter_driver_altitudes
VALUES
('Copy Protection', 'SnEraser.sys', 65300, 'Informzaschita');
INSERT INTO dbo.filter_driver_altitudes
VALUES
('Copy Protection', 'vfilter.sys', 65200, 'RSJ Software GmbH');
INSERT INTO dbo.filter_driver_altitudes
VALUES
('Copy Protection', 'COGOFlt32.sys', 65100, 'Fortium Technologies Ltd');
INSERT INTO dbo.filter_driver_altitudes
VALUES
('Copy Protection', 'COGOFlt64.sys', 65100, 'Fortium Technologies Ltd');
INSERT INTO dbo.filter_driver_altitudes
VALUES
('Copy Protection', 'COGOFLTia64.sys', 65100, 'Fortium Technologies Ltd');
INSERT INTO dbo.filter_driver_altitudes
VALUES
('Copy Protection', 'scrubber.sys', 65000, 'Microsoft');
INSERT INTO dbo.filter_driver_altitudes
VALUES
('Copy Protection', 'SmDLP.sys', 64100, 'SmTools');
INSERT INTO dbo.filter_driver_altitudes
VALUES
('Copy Protection', 'BRDriver.sys', 64000, 'BitRaider LLC');
INSERT INTO dbo.filter_driver_altitudes
VALUES
('Copy Protection', 'BRDriver64.sys', 64000, 'BitRaider LLC');
INSERT INTO dbo.filter_driver_altitudes
VALUES
('Copy Protection', 'X7Ex.sys', 62500, 'Exent Technologies Ltd');
INSERT INTO dbo.filter_driver_altitudes
VALUES
('Copy Protection', 'LibertyFSF.sys', 62300, 'Bayalink Solutions Co');
INSERT INTO dbo.filter_driver_altitudes
VALUES
('Copy Protection', 'axfsdrv2.sys', 62100, 'Axence Software Inc.');
INSERT INTO dbo.filter_driver_altitudes
VALUES
('Copy Protection', 'sds.sys', 62000, 'Egress Software');
INSERT INTO dbo.filter_driver_altitudes
VALUES
('Copy Protection', 'zzenc.sys', 61650.5, 'Imdtech LLC');
INSERT INTO dbo.filter_driver_altitudes
VALUES
('Copy Protection', 'TotalSystemAuditor.sys', 61600, 'ANRC LLC');
INSERT INTO dbo.filter_driver_altitudes
VALUES
('Copy Protection', 'MBAMApiary.sys', 61500, 'Malwarebytes Corp.');
INSERT INTO dbo.filter_driver_altitudes
VALUES
('Copy Protection', 'WA_FSW.sys', 61400, 'Programas Administración y Mejoramiento');
INSERT INTO dbo.filter_driver_altitudes
VALUES
('Copy Protection', 'ViewIntus_RTAS', 61300, 'Pentego Technologies');
INSERT INTO dbo.filter_driver_altitudes
VALUES
('Copy Protection', 'tffac.sys', 61200, 'Toshiba Corporation');
INSERT INTO dbo.filter_driver_altitudes
VALUES
('Copy Protection', 'tccp.sys', 61100, 'TrusCont Ltd');
INSERT INTO dbo.filter_driver_altitudes
VALUES
('Copy Protection', 'KomFS.sys', 61000, 'KOM Networks');
INSERT INTO dbo.filter_driver_altitudes
VALUES
('Bottom', 'RMPFileMounter.sys', 48000, 'ManageEngine Zoho');
INSERT INTO dbo.filter_driver_altitudes
VALUES
('Bottom', 'MFPAMCtrl.sys', 47500, 'Micro Focus');
INSERT INTO dbo.filter_driver_altitudes
VALUES
('Bottom', 'cbfsfilter2017.sys', 47400, '12d Synergy');
INSERT INTO dbo.filter_driver_altitudes
VALUES
('Bottom', 'pfmfs_???.sys', 47300, 'Pismo Technic Inc.');
INSERT INTO dbo.filter_driver_altitudes
VALUES
('Bottom', 'AlfaVS.sys', 47290.5, 'AlfaSP.com');
INSERT INTO dbo.filter_driver_altitudes
VALUES
('Bottom', 'DLDriverMiniFlt.sys', 47200, 'Acronis');
INSERT INTO dbo.filter_driver_altitudes
VALUES
('Bottom', 'DLPDriverProt.sys', 47199.5, 'Acronis');
INSERT INTO dbo.filter_driver_altitudes
VALUES
('Bottom', 'hsmltlib.sys', 47110, 'Hitachi Solutions');
INSERT INTO dbo.filter_driver_altitudes
VALUES
('Bottom', 'hskdlib.sys', 47100, 'Hitachi Solutions');
INSERT INTO dbo.filter_driver_altitudes
VALUES
('Bottom', 'acmnlib.sys', 47090, 'Hitachi Solutions');
INSERT INTO dbo.filter_driver_altitudes
VALUES
('Bottom', 'aictracedrv_b.sys', 47000, 'AI Consulting');
INSERT INTO dbo.filter_driver_altitudes
VALUES
('Bottom', 'SBox.sys', 46950, 'ASF Labs 2019 LTD');
INSERT INTO dbo.filter_driver_altitudes
VALUES
('Bottom', 'hhdcfltr.sys', 46900, 'Seagate Technology');
INSERT INTO dbo.filter_driver_altitudes
VALUES
('Bottom', 'AmdFSMini.sys', 46890.5, 'Advanced Micro Devices');
INSERT INTO dbo.filter_driver_altitudes
VALUES
('Bottom', 'Npsvctrig.sys', 46000, 'Microsoft');
INSERT INTO dbo.filter_driver_altitudes
VALUES
('Bottom', 'klvfs.sys', 44900, 'Kaspersky Lab');
INSERT INTO dbo.filter_driver_altitudes
VALUES
('Bottom', 'klbackupflt.sys', 44890, 'Kaspersky Lab');
INSERT INTO dbo.filter_driver_altitudes
VALUES
('Bottom', 'rsfxdrv.sys', 41000, 'Microsoft');
INSERT INTO dbo.filter_driver_altitudes
VALUES
('Bottom', 'defilter.sys', 40900, 'Microsoft');
INSERT INTO dbo.filter_driver_altitudes
VALUES
('Bottom', 'AppVVemgr.sys', 40800, 'Microsoft');
INSERT INTO dbo.filter_driver_altitudes
VALUES
('Bottom', 'wofadk.sys', 40730, 'Microsoft');
INSERT INTO dbo.filter_driver_altitudes
VALUES
('Bottom', 'wof.sys', 40700, 'Microsoft');
INSERT INTO dbo.filter_driver_altitudes
VALUES
('Bottom', 'fileinfo', 40500, 'Microsoft');
INSERT INTO dbo.filter_driver_altitudes
VALUES
('Bottom', 'WinSetupBoot.sys', 40400, 'Microsoft');
INSERT INTO dbo.filter_driver_altitudes
VALUES
('Bottom', 'WinSetupMon.sys', 40300, 'Microsoft');
INSERT INTO dbo.filter_driver_altitudes
VALUES
('brokering FS', 'bfs.sys', 150000, 'Microsoft');
COMMIT TRAN;


--Update the tbl_fltmc_filters and tbl_fltmc_instances with addition info from lookup table above

--tbl_fltmc_filters


DECLARE @query NVARCHAR(MAX);
DECLARE @query2 NVARCHAR(MAX);

IF (OBJECT_ID('tbl_fltmc_filters') IS NOT NULL)
BEGIN

    ALTER TABLE dbo.tbl_fltmc_filters ADD FilterType NVARCHAR(96) NULL;
    ALTER TABLE dbo.tbl_fltmc_filters ADD Minifilter NVARCHAR(128) NULL;
    ALTER TABLE dbo.tbl_fltmc_filters ADD Company NVARCHAR(256) NULL;

    SET @query
        = N'UPDATE dbo.tbl_fltmc_filters
                 SET FilterType = f2.FilterType,
                     Minifilter = f2.Minifilter,
                     Company = f2.Company
                 FROM dbo.tbl_fltmc_filters f1
                     INNER JOIN dbo.filter_driver_altitudes f2
                       ON (f1.Altitude = f2.Altitude);';
    EXEC SP_EXECUTESQL @query;
END;


--tbl_fltmc_instances

IF (OBJECT_ID('tbl_fltmc_instances') IS NOT NULL)
BEGIN

    ALTER TABLE dbo.tbl_fltmc_instances ADD FilterType NVARCHAR(96) NULL;
    ALTER TABLE dbo.tbl_fltmc_instances ADD Minifilter NVARCHAR(128) NULL;
    ALTER TABLE dbo.tbl_fltmc_instances ADD Company NVARCHAR(256) NULL;

    SET @query2
        = N'UPDATE dbo.tbl_fltmc_instances
                  SET FilterType = f2.FilterType,
                      Minifilter = f2.Minifilter,
                      Company = f2.Company
                  FROM dbo.tbl_fltmc_instances f1
                      INNER JOIN dbo.filter_driver_altitudes f2
                        ON (f1.Altitude = f2.Altitude);';
    EXEC SP_EXECUTESQL @query2;
END;

