using System;
using System.Collections.Generic;
using System.Text;
using System.Deployment.Application;
using System.Diagnostics;
using System.ComponentModel;
using System.Windows.Forms;

namespace sqlnexus
{
    class ClickOnce
    {
        public TraceSource TraceLogger; 

        //// This will do a synchronous (blocking) update
        //private void CheckForUpdates()
        //{
        //    Boolean updateAvailable = false;
        //    try
        //    {
        //        ApplicationDeployment ad = ApplicationDeployment.CurrentDeployment;
        //        LogMessage("ClickOnce: Current application deployment version: " + ad.CurrentVersion.ToString());
        //        LogMessage("ClickOnce: Checking for updates at " + ad.UpdateLocation.ToString());
        //        updateAvailable = ad.CheckForUpdate();
        //        if (updateAvailable)
        //            LogMessage("CheckForUpdate: There is an application update available.");
        //        else
        //            LogMessage("CheckForUpdate: There is not an application update available.");
        //    }
        //    catch (DeploymentDownloadException dde)
        //    {   // This exception occurs if a network error or disk error occurs when downloading the deployment. 
        //        LogMessage("The application cannot check for the existence of a new version at this time. Please check your network connection, or try again later. Error: " + dde.Message);
        //    }
        //    catch (InvalidDeploymentException ide)
        //    {
        //        LogMessage("The application cannot check for an update. Either the app is not running under ClickOnce, or the ClickOnce deployment is corrupt. Please redeploy the application and try again. Error: " + ide.Message);
        //    }
        //    catch (InvalidOperationException ioe)
        //    {
        //        LogMessage("This application cannot check for an update. This most often happens if the application is already in the process of updating. Error: " + ioe.Message);
        //    }
        //    catch (Exception ex)
        //    {
        //        LogMessage("An unexpected exception occurred while checking for an update: " + ex.ToString());
        //    }
        //    return;
        //}

        private void LogMessage (string msg, TraceEventType eventtype)
        {
            try
            {
                TraceLogger.TraceEvent(eventtype, 0, msg);
            }
            catch
            {
            }
        }

        private void LogMessage (string msg)
        {
            LogMessage(msg, TraceEventType.Information);
        }
        
        public void UpdateApplicationAsync()
        {
            try
            {
                if (ApplicationDeployment.IsNetworkDeployed)
                {
                    ApplicationDeployment ad = ApplicationDeployment.CurrentDeployment;
                    LogMessage("AutoUpdate: Checking for updates at " + ad.UpdateLocation.ToString());
                    LogMessage("AutoUpdate: Current application deployment version: " + ad.CurrentVersion.ToString());
                    ad.CheckForUpdateCompleted += new CheckForUpdateCompletedEventHandler(ad_CheckForUpdateCompleted);
                    ad.CheckForUpdateProgressChanged += new DeploymentProgressChangedEventHandler(ad_CheckForUpdateProgressChanged);

                    ad.CheckForUpdateAsync();
                }
                else
                {
                    LogMessage("AutoUpdate: Application is not running as part of a ClickOnce deployment");
                }
            }
            catch (DeploymentDownloadException dde)
            {   // This exception occurs if a network error or disk error occurs when downloading the deployment. 
                LogMessage("AutoUpdate: The application cannot check for the existence of a new version at this time. Please check your network connection, or try again later. Error: " + dde.Message);
                return;
            }
            catch (InvalidDeploymentException ide)
            {
                LogMessage("AutoUpdate: The application cannot check for an update. Either the app is not running under ClickOnce, or the ClickOnce deployment is corrupt. Please redeploy the application and try again. Error: " + ide.Message);
                return;
            }
            catch (InvalidOperationException ioe)
            {
                LogMessage("AutoUpdate: This application cannot check for an update. This most often happens if the application is already in the process of updating. Error: " + ioe.Message);
                return;
            }
            catch (Exception ex)
            {
                LogMessage("AutoUpdate: An unexpected exception occurred while checking for an update: " + ex.ToString());
                return;
            }
        }

        void ad_CheckForUpdateProgressChanged(object sender, DeploymentProgressChangedEventArgs e)
        {
            LogMessage(String.Format("AutoUpdate: Downloading: {0}. {1:D}K of {2:D}K downloaded.", GetProgressString(e.State), e.BytesCompleted / 1024, e.BytesTotal / 1024));
        }

        string GetProgressString(DeploymentProgressState state)
        {
            if (state == DeploymentProgressState.DownloadingApplicationFiles)
                return "application files";
            else if (state == DeploymentProgressState.DownloadingApplicationInformation)
                return "application manifest";
            else
                return "deployment manifest";
        }

        void ad_CheckForUpdateCompleted(object sender, CheckForUpdateCompletedEventArgs e)
        {
            if (e.Error != null)
            {
                LogMessage("AutoUpdate: ERROR: Could not retrieve new version of the application. Reason: \n" + e.Error.Message + "\nPlease report this error to the system administrator.");
                return;
            }
            else if (e.Cancelled == true)
            {
                LogMessage("AutoUpdate: The update was cancelled.");
                return;
            }

            try
            {
                LogMessage("AutoUpdate: Latest available version: " + e.AvailableVersion.ToString());
            }
            catch
            { // This will throw "Update not available" if the current installed deployment version is up-to-date
                LogMessage("AutoUpdate: Latest available version is installed");
            }

            // Ask the user if they would like to update the application now.
            if (e.UpdateAvailable)
            {
                LogMessage("AutoUpdate: An update is available (version " + e.AvailableVersion.ToString()
                    + ", " + e.UpdateSizeBytes.ToString() + " bytes).");

                //if (!e.IsUpdateRequired)
                //{
                //    DialogResult dr = MessageBox.Show(
                //        "An update is available. Would you like to update the application now?\n\n"
                //        + "The update will be installed in the background, and will be active \n"
                //        + "the next time you launch the application from the Start menu.",
                //        "Update Available", MessageBoxButtons.OKCancel);
                //    if (DialogResult.OK == dr)
                //    {
                //        BeginUpdate();
                //    }
                //}
                //else
                //{
                //    MessageBox.Show("A mandatory update is available for your application. The update will be installed in the background, \n"
                //        + "and will be active the next time you launch the application.", "Mandatory Update Available", MessageBoxButtons.OK);
                //    BeginUpdate();
                //}
            }
            else
            {
                LogMessage("AutoUpdate: No updates are available.");
            }
        }

        private void BeginUpdate()
        {
            LogMessage("AutoUpdate: Starting asynch update...");
            ApplicationDeployment ad = ApplicationDeployment.CurrentDeployment;
            ad.UpdateCompleted += new AsyncCompletedEventHandler(ad_UpdateCompleted);
            ad.UpdateProgressChanged += new DeploymentProgressChangedEventHandler(ad_UpdateProgressChanged);

            ad.UpdateAsync();
        }

        void ad_UpdateProgressChanged(object sender, DeploymentProgressChangedEventArgs e)
        {
            LogMessage(String.Format("AutoUpdate: {0:D}K out of {1:D}K downloaded - {2:D}% complete", e.BytesCompleted / 1024, e.BytesTotal / 1024, e.ProgressPercentage));
        }

        void ad_UpdateCompleted(object sender, AsyncCompletedEventArgs e)
        {
            if (e.Cancelled)
            {
                LogMessage("AutoUpdate: The update of the application's latest version was cancelled.");
                return;
            }
            else if (e.Error != null)
            {
                LogMessage("AutoUpdate: ERROR: Could not install the latest version of the application. Reason: \n" + e.Error.Message + "\nPlease report this error to the system administrator.");
                return;
            }

            LogMessage("AutoUpdate: The application has been updated.");

            //DialogResult dr = MessageBox.Show("The application has been updated. Restart? (If you do not restart now, the new version will not take effect until after you quit and launch the application again.)", "Restart Application", MessageBoxButtons.OKCancel);
            //if (DialogResult.OK == dr)
            //{
            //    Application.Restart();
            //}
        }
    }
}
