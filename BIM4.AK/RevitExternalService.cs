using Autodesk.Revit.Attributes;
using Autodesk.Revit.DB;
using Autodesk.Revit.DB.Mechanical;
using Autodesk.Revit.DB.Structure;
using Autodesk.Revit.UI;
using BIM4.AK.Properties;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.ServiceModel;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace BIM4.AK
{
#pragma warning disable CS0649

    [Transaction(TransactionMode.Manual)]
    [Regeneration(RegenerationOption.Manual)]
    [ServiceBehavior(InstanceContextMode = InstanceContextMode.Single)]
    internal class RevitExternalService : IRevitExternalService
    {
        private readonly string currentDocumentPath;
        private (string DocID, string DocName) StateInfo;

        private readonly Dictionary<(string, string, double), List<(string, string, string)>> rooms =
            new Dictionary<(string, string, double), List<(string, string, string)>>();

        public string Currentbuild { get; private set; }
        private readonly Dictionary<string, string> Levels = new Dictionary<string, string>();
        private static readonly object _locker = new object();
        private List<(string, string)> IDPannelList;
        private const int WAIT_TIMEOUT = 5000; // 5 seconds timeout
        private UIApplication _uiapp;

        internal UIApplication uiapp
        {
            get => _uiapp;
            set
            {
                if (value == null) return;
                _uiapp = value;
            }
        }

        private Dictionary<string, List<string>> FamilyTypes;

        private List<(string GUID, string PannelName, string Level, string Room, string Power, string Family, string
            FamilyType, string Attrib, string AttribValue, double Height)> inputPannelList;

        private int placeresult = -1;

        #region Implementation of IRevitExternalService

        public string GetCurrentDocumentPath()
        {
            Debug.Print("{0}: {1}", Resources.PushTaskToTheContainer, DateTime.Now.ToString("HH:mm:ss.fff"));
            lock (_locker)
            {
                TaskContainer.Instance.EnqueueTask(GetDocumentPath);
                // Wait when the task is completed
                Monitor.Wait(_locker, WAIT_TIMEOUT);
            }
            Debug.Print("{0}: {1}", Resources.FinishTask, DateTime.Now.ToString("HH:mm:ss.fff"));
            return currentDocumentPath;
        }
        public string GetDocumentID()
        {
            string docID = string.Empty;
            lock (_locker)
            {
                TaskContainer.Instance.EnqueueTask(uiApplication =>
                {
                    try
                    {
                        UIDocument uidoc = uiApplication.ActiveUIDocument;
                        Document doc = uidoc.Document;
                        docID = doc.ProjectInformation.UniqueId;
                    }
                    finally
                    {
                        lock (_locker)
                        {
                            Monitor.Pulse(_locker);
                        }
                    }
                });
                Monitor.Wait(_locker, WAIT_TIMEOUT);
                Debug.Print("{0}: {1}", Resources.FinishTask, DateTime.Now.ToString("HH:mm:ss.fff"));
            }

            return docID;
        }

        public (string, string) GetStateInfo()
        {
            Debug.Print("{0}: {1}", Resources.PushTaskToTheContainer, DateTime.Now.ToString("HH:mm:ss.fff"));
            lock (_locker)
            {
                while (uiapp == null)
                {
                    TaskContainer.Instance.EnqueueTask(Uapplication => { uiapp = Uapplication; });
                }

                if (!string.IsNullOrEmpty(StateInfo.DocID)) return StateInfo;
                StateInfo = CheckStateInfo(uiapp);
                //Monitor.Wait(_locker, 5000);


                // Wait when the task is completed
            }

            Debug.Print("{0}: {1}", Resources.FinishTask, DateTime.Now.ToString("HH:mm:ss.fff"));

            return StateInfo;
        }

        private void GetDocumentPath(UIApplication uiApplication)
        {
            try
            {
                Currentbuild = uiApplication.Application.VersionBuild;
            }
            // Always release locker in finally block
            // to ensure to unlock locker object.
            finally
            {
                lock (_locker)
                {
                    Monitor.Pulse(_locker);
                }
            }
        }

        private void Rooms(UIApplication uiApplication)
        {

            try
            {
                UIDocument uidoc = uiApplication.ActiveUIDocument;
                Document doc = uidoc.Document;
                FilteredElementCollector Spaces = new FilteredElementCollector(doc)
                    .OfClass(typeof(SpatialElement)); //забираем все помещения
                FilteredElementCollector collector = new FilteredElementCollector(doc);
                ICollection<Element> levels = collector.OfClass(typeof(Level)).ToElements();

                if (levels.Count <= 0) return;
                foreach (Element lvl in levels)
                {
                    Level level = (Level)lvl;
                    //забираем все spatial которые находятся на уровне
                    var elements = (from SpatialElement el in Spaces
                                    where el.Level.Name == level.Name
                                    where (BuiltInCategory)el.Category.Id.IntegerValue == BuiltInCategory.OST_MEPSpaces
                                    select el)
                        .ToList();
                    //List<SpatialElement> Listelements = elements.Cast<SpatialElement>().ToList();
                    if (elements.Count == 0) continue;

                    var s = elements
                        .Cast<Element>()
                        .Select(e =>
                        {
                            string s1 = string.Empty;
                            string s2 = string.Empty;
                            string s3 = e.Id.ToString();


                            foreach (Parameter parameter in e.Parameters)
                            {
                                switch (parameter.Definition.Name)
                                {
                                    case "Имя":
                                        s1 = parameter.AsString();
                                        break;

                                    case "Номер":
                                        s2 = string.IsNullOrEmpty(parameter.AsString()) ? "-" : parameter.AsString();
                                        break;
                                }

                                if (s2 == "-" &&
                                    parameter.Definition.Name == "Номер" &&
                                    !string.IsNullOrEmpty(parameter.AsString()))
                                    s2 = parameter.AsString();
                            }

                            return (s1, s2, s3);
                        })
                        .ToList();


                    List<(string, string, string)> rl = new List<(string, string, string)>();


                    if (s.Count > 0)
                    {
                        foreach (var (s1, s2, s3) in s)
                        {
                            (string RoomID, string RoomNum, string RoomNam) rls;
                            rls.RoomNam = s1;
                            rls.RoomNum = s2;
                            rls.RoomID = s3;
                            rl.Add(rls);
                        }

                    }

                    (string LevelID, string LevelName, double Elevation) lvls;
                    lvls.LevelID = lvl.Id.ToString();
                    lvls.LevelName = lvl.Name;
                    lvls.Elevation = level.Elevation;

                    if (!rooms.ContainsKey(lvls))
                    {
                        rooms.Add(lvls, rl);
                    }
                }
            }
            finally
            {
                lock (_locker)
                {
                    Monitor.Pulse(_locker);
                }
            }
        }

        public static Dictionary<string, List<string>> FindFamilyTypes(Document doc, BuiltInCategory cat)
        {

            return new FilteredElementCollector(doc)
                .WherePasses(new ElementClassFilter(typeof(FamilySymbol)))
                .WherePasses(new ElementCategoryFilter(cat))
                .Cast<FamilySymbol>()
                .GroupBy(e => e.Family.Name)
                .ToDictionary(e => e.Key, e => e.Select(s => s.Name).ToList());
        }

        private (string, string) CheckStateInfo(UIApplication uiApplication)
        {
            StateInfo.DocID = string.Empty;
            StateInfo.DocID = string.Empty;
            try
            {
                UIDocument uidoc = uiApplication.ActiveUIDocument;
                Document doc = uidoc.Document;
                string doctitle = doc.Title;
                string docid = doc.ProjectInformation.UniqueId;
                StateInfo.DocID = docid;
                StateInfo.DocName = doctitle;
            }
            finally
            {
                lock (_locker)
                {
                    Monitor.Pulse(_locker);
                }
            }
            return StateInfo;
        }

        public Dictionary<string, string> GetLevels()
        {
            lock (_locker)
            {
                TaskContainer.Instance.EnqueueTask(uiApplication =>
                {
                    try
                    {
                        UIDocument uidoc = uiApplication.ActiveUIDocument;
                        Document doc = uidoc.Document;
                        FilteredElementCollector collector = new FilteredElementCollector(doc);
                        ICollection<Element> levels = collector.OfClass(typeof(Level)).ToElements();

                        foreach (Element el in levels)
                        {
                            Level level = (Level)el;
                            string levelID = level.Id.ToString();
                            if (!Levels.ContainsKey(levelID))
                            {
                                Levels.Add(levelID, level.Name); //словарь содержит еще и ID уровней :)))
                            }
                        }
                    }
                    finally
                    {
                        lock (_locker)
                        {
                            Monitor.Pulse(_locker);
                        }
                    }
                });
            }

            return Levels;
        }

        public async void SetUIApp()
        {

            try
            {
                await Task.Factory.StartNew(() =>
                {
                    TaskContainer.Instance.EnqueueTask(uIApplication => { uiapp = uIApplication; });
                });
            }
            catch
            {

            }



        }

        public Dictionary<(string, string, double), List<(string, string, string)>> GetRooms()
        {


            lock (_locker)
            {
                SetUIApp();
                //if (rooms.Count != 0) return rooms;
                if (uiapp != null)
                {
                    Rooms(uiapp);
                }

            }

            Debug.Print("{0}: {1}", Resources.FinishTask, DateTime.Now.ToString("HH:mm:ss.fff"));

            return rooms;
        }

        public int LoadPannels(List<(string GUID, string PannelName, string Level, string Room, string Power, string Family, string FamilyType, string Attrib, string AttribValue, double Height)> inputInfo)
        {
            int res = -1;
            inputPannelList = inputInfo;
            res = 1;
            return res;

        }

        public int PlacePannels()
        {
            int res = -1;
            lock (_locker)
            {
                TaskContainer.Instance.EnqueueTask(_PlacePannels);
                Debug.Print("{0}: {1}", Resources.FinishTask, DateTime.Now.ToString("HH:mm:ss.fff"));
                res = 1;

            }

            return res;
        }
        public void _PlacePannels(UIApplication ap)
        {

            IDPannelList = new List<(string, string)>();

            //Transaction tx = null;
            placeresult = 1;
            try
            {

                Document doc = ap.ActiveUIDocument.Document;
                using (var tx = new Transaction(doc, "PlacePannels"))
                {
                    tx.Start();
                    Dictionary<string, List<FamilySymbol>> elements = new FilteredElementCollector(doc)
                        .WherePasses(new ElementClassFilter(typeof(FamilySymbol)))
                        .WherePasses(new ElementCategoryFilter(BuiltInCategory.OST_ElectricalEquipment))
                        .Cast<FamilySymbol>()
                        .GroupBy(e => e.Family.Name)
                        .ToDictionary(e => e.Key, e => e.ToList());

                    foreach (var RevitInfo in this.inputPannelList)
                    {
                        IEnumerable<FamilySymbol> familySymbols = from el in elements
                                                                  where el.Key == RevitInfo.Family
                                                                  select el.Value
                                                                      .FirstOrDefault(famsymbol => famsymbol.Name == RevitInfo.FamilyType);

                        FamilySymbol familySymbol = familySymbols?.First();

                        if (familySymbol == null) continue;
                        int idInt = Convert.ToInt32(RevitInfo.Room);

                        ElementId id = new ElementId(Convert.ToInt32(RevitInfo.Room));
                        Element eFromId = doc.GetElement(id);
                        if (!(eFromId is Space space)) continue;

                        XYZ xYZ = GetElementCenter(eFromId); //(space.Location as LocationPoint).Point;
                        XYZ xY = new XYZ(xYZ.X, xYZ.Y, 0);

                        Level level = space.Level;
                        if (!(space.Location is LocationPoint)) continue;
                        FamilyInstance familyInstance = doc.Create.NewFamilyInstance(xY, familySymbol, level,
                            StructuralType.NonStructural);
                        Parameter GKS_SS_SystemParam = familyInstance.LookupParameter(RevitInfo.Attrib);
                        Parameter OffsetParam = familyInstance.LookupParameter("Смещение");
                        Parameter MarkaParam = familyInstance.LookupParameter("Марка");
                        GKS_SS_SystemParam?.Set(RevitInfo.AttribValue);
                        OffsetParam?.Set(RevitInfo.Height / 304.8);
                        MarkaParam?.Set(RevitInfo.PannelName);
                        (string GUID, string ID) returninfo;
                        returninfo.GUID = RevitInfo.GUID;
                        returninfo.ID = familyInstance.Id.ToString();
                        IDPannelList.Add(returninfo);
                    }

                    tx.Commit();
                    placeresult = IDPannelList.Count > 0 ? 2 : 3;
                }

            }

            catch (Exception ex)
            {
                // tx?.RollBack();
                placeresult = 3;
                Monitor.Pulse(_locker);

            }
            finally
            {
                lock (_locker)
                {
                    Monitor.Pulse(_locker);
                }
            }
        }


        public XYZ GetElementCenter(Element elem)
        {
            BoundingBoxXYZ bounding = elem.get_BoundingBox(null);
            XYZ center = (bounding.Max + bounding.Min) * 0.5;

            return center;
        }

        public Dictionary<string, List<string>> GetFamilyTypes()
        {

            lock (_locker)
            {
                try
                {
                    SetUIApp();
                    if (FamilyTypes?.Count > 0) FamilyTypes.Clear();
                    if (uiapp != null)
                    {
                        var doc = uiapp.ActiveUIDocument.Document;
                        FamilyTypes = FindFamilyTypes(doc, BuiltInCategory.OST_ElectricalEquipment);
                    }
                }
                catch
                {

                }
                finally
                {
                    lock (_locker)
                    {
                        Monitor.Pulse(_locker);
                    }
                }


            }


            return FamilyTypes;
        }

        public List<(string, string)> GetFamilyListID()
        {
            return IDPannelList;
        }

        public int GetPlaceResult()
        {
            return placeresult;
        }

        #endregion Implementation of IRevitExternalService
    }
}