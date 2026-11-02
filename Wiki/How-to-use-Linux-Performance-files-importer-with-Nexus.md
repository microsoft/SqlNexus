1. Once you have downloaded (and extracted if .tar file) your \output from [Pssdiag for SQL Server on Linux](https://github.com/microsoft/DiagManager/wiki/Run-PSSDiag-for-SQL-Server-on-Linux) collection, make sure that the **pssdiag_collector.conf** and **pssdiag_importer.conf** files are available in the \output folder
1. To import SQL Server Linux performance data collected via [Pssdiag for SQL Server on Linux](https://github.com/microsoft/DiagManager/wiki/Run-PSSDiag-for-SQL-Server-on-Linux#downloading-and-executing-pssdiag), use the regular process to point SQL Nexus to the output folder and run the import - see [SQL Nexus Importing data](https://github.com/microsoft/SqlNexus/wiki/How-to-use-SQL-Nexus#importing-data-and-analyzing)
1. As part of the import, to convert of Linux *.perf files into a Windows *.BLG format, you can enable Linux Performance File Importer

  ![image](https://github.com/user-attachments/assets/1d2917da-5262-4799-9d17-6e753a45236e)

1. Once SQL Nexus import completes, you will find several .BLG and .TSV files that represent the performance data collected from the Linux machine. For example:

  ![image](https://github.com/user-attachments/assets/d43963da-71fe-4663-a21d-5840dbba9b6e)

-  Also, you will find a single merged BLG file that includes all the counter files under \output\Single_BLG folder
  
  ![image](https://github.com/user-attachments/assets/e972a3e9-dcda-45f4-9b7f-1001e2bbce2e)





