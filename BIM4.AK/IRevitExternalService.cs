using System.Collections.Generic;
using System.ServiceModel;

namespace BIM4.AK
{
    [ServiceContract]
    public interface IRevitExternalService
    {
        [OperationContract]
        string GetCurrentDocumentPath();

        [OperationContract]
        Dictionary<string, string> GetLevels();

        [OperationContract]
        Dictionary<(string, string, double), List<(string, string, string)>> GetRooms();

        [OperationContract]
        string GetDocumentID();

        [OperationContract]
        (string, string) GetStateInfo();

        [OperationContract]
        int LoadPannels(List<(string GUID, string PannelName, string Level, string Room, string Power, string Family, string FamilyType, string Attrib, string AttribValue, double Height)> IDPannelList);

        [OperationContract]
        int PlacePannels();

        [OperationContract]
        Dictionary<string, List<string>> GetFamilyTypes();

        [OperationContract]
        List<(string, string)> GetFamilyListID();

        [OperationContract]
        int GetPlaceResult();
        [OperationContract]
        void SetUIApp();
    }
}
