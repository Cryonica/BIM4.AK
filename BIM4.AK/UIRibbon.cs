using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using Autodesk.Revit.UI.Events;
using System;
using System.Diagnostics;
using System.IO;
using System.ServiceModel;
using System.Windows.Forms;
using System.Windows.Media.Imaging;
using System.Xml;

namespace BIM4.AK
{
    public class UIRibbon
    {
        private const string serviceUrl = "net.tcp://localhost:6565/";

        internal static ServiceHost serviceHost;
        public static void LoadPlugin()
        {
            UIControlledApplication application = BIM4.Utils.ActiveUIControlledApplication;
            string tabName = "4BIM(AK)";
            try
            {
                application.CreateRibbonTab(tabName);
            }
            catch { }

            string ribbonpath = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData) +
                @"\Autodesk\Revit\Addins\2020\ASU\";
            string text = ribbonpath;

            string text3 = Path.Combine(ribbonpath, "Ribbon.xml");
            bool flag6 = File.Exists(text3);
            bool flag7 = !flag6;

           
            if (flag7)
            {
                TaskDialog.Show("Ошибка", "Панель GKSASU не установлена. Не найден файл \n" + text3);
                
            }
            else
            {
                XmlDocument xmlDocument = new XmlDocument();
                xmlDocument.Load(text3);
                XmlElement documentElement = xmlDocument.DocumentElement;
                XmlNodeList childNodes = documentElement.ChildNodes;
                foreach (object obj in childNodes)
                {
                    XmlNode xmlNode = (XmlNode)obj;
                    string innerText = xmlNode.SelectSingleNode("Title").InnerText;
                    RibbonPanel orCreatePanel = RibbonSupport.GetOrCreatePanel(application, tabName, innerText);
                    XmlNode xmlNode2 = xmlNode.SelectSingleNode("Buttons");
                    XmlNodeList childNodes2 = xmlNode2.ChildNodes;
                    bool flaggks = false;
                    try
                    {
                        foreach (object obj2 in childNodes2)
                        {
                            XmlNode xmlNode3 = (XmlNode)obj2;
                            string name = xmlNode3.Name;
                            string innerText2 = xmlNode3.SelectSingleNode("Title").InnerText;
                            string text4 = xmlNode3.SelectSingleNode("AssemblyPath").InnerText;
                            text4 = Path.Combine(text, text4);
                            string innerText3 = xmlNode3.SelectSingleNode("ClassName").InnerText;
                            string text5 = xmlNode3.SelectSingleNode("LargeImagePath").InnerText;
                            text5 = Path.Combine(text, text5);
                            string text6 = xmlNode3.SelectSingleNode("SmallImagePath").InnerText;
                            text6 = Path.Combine(text, text6);
                            string innerText4 = xmlNode3.SelectSingleNode("Tooltip").InnerText;
                            PushButton pushButton = null;
                            try
                            {
                                PushButtonData buttonData = new PushButtonData(
                                    name,
                                    innerText2,
                                    text4,
                                    innerText3
                                    );
                                pushButton = orCreatePanel.AddItem(buttonData) as PushButton;
                                if (innerText2 == "GKSTOOLS")
                                    flaggks = true;
                            }
                            catch
                            {
                            }
                            try
                            {
                                pushButton.LargeImage = new BitmapImage(new Uri(text5));
                            }
                            catch { }

                            try
                            {
                                pushButton.Image = new BitmapImage(new Uri(text6));
                            }
                            catch { }

                            pushButton.ToolTip = innerText4;

                            if (flaggks)
                            {
                            }
                        }
                    }
                    catch (Exception e)
                    {
                        MessageBox.Show(e.StackTrace);
                    }
                }

                bool res = RunWCFServer(application);
                
            }
           
        }
        private static bool RunWCFServer(UIControlledApplication a)
        {
            a.Idling += OnIdling;

            Uri uri = new Uri(serviceUrl);
            serviceHost = new ServiceHost(typeof(RevitExternalService), uri);
            var binding2 = new BasicHttpBinding
            {
                MaxReceivedMessageSize = 2147483647,
                MaxBufferSize = 2147483647,
                MaxBufferPoolSize = 524288
            };

            NetTcpBinding binding = new NetTcpBinding(SecurityMode.None)
            {
                MaxBufferPoolSize = 2147483647,
                MaxBufferSize = 2147483647,
                MaxReceivedMessageSize = 2147483647,
                CloseTimeout = TimeSpan.FromSeconds(1),
                OpenTimeout = TimeSpan.FromSeconds(1),
                ReceiveTimeout = TimeSpan.FromSeconds(10),
                SendTimeout = TimeSpan.FromSeconds(1),
                HostNameComparisonMode = HostNameComparisonMode.StrongWildcard,
                Name = "AOVGENHOST",
                ReaderQuotas =
                {
                    MaxStringContentLength = 2147483647,
                    MaxArrayLength = 2147483647,
                    MaxDepth = 64,
                    MaxBytesPerRead = 2147483647
                }
            };


            serviceHost.AddServiceEndpoint(typeof(IRevitExternalService), binding, "");

            serviceHost.Open();

            return (serviceHost.State == CommunicationState.Opened);
        }
        private static void OnIdling(object sender, IdlingEventArgs e)
        {
            if (serviceHost.State != CommunicationState.Opened) serviceHost.Open();

            var uiApp = sender as UIApplication;



            Debug.Print("OnIdling: {0}", DateTime.Now.ToString("HH:mm:ss.fff"));

            // be carefull. It loads CPU
            //e.SetRaiseWithoutDelay();


            if (!TaskContainer.Instance.HasTaskToPerform)
                return;

            try
            {
                Debug.Print("{0}: {1}", Properties.Resources.StartExecuteTask, DateTime.Now.ToString("HH:mm:ss.fff"));

                var task = TaskContainer.Instance.DequeueTask();
                task(uiApp);

                Debug.Print("{0}: {1}", Properties.Resources.EndExecuteTask, DateTime.Now.ToString("HH:mm:ss.fff"));
            }
            catch (Exception ex)
            {
                uiApp.Application.WriteJournalComment(
                    string.Format("RevitExternalService. {0}:\r\n{2}",
                    Properties.Resources.AnErrorOccuredWhileExecutingTheOnIdlingEven,
                    ex.ToString()), true);

                Debug.WriteLine(ex);
            }

            //e.SetRaiseWithoutDelay();
        }

        [STAThread]
        public Result Execute(ExternalCommandData commandData, ref string message, ElementSet elements)
        {
            checked
            {
                Result result;
                try
                {
                    result = Result.Succeeded;
                }
                catch
                {
                    result = Result.Failed;
                }
                return result;
            }
        }
    }
}
