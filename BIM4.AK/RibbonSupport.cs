using Autodesk.Revit.UI;
using System.Collections.Generic;
using System.Linq;

namespace BIM4.AK
{
    public static class RibbonSupport
    {
        public static RibbonPanel GetOrCreatePanel(UIControlledApplication uiApp, string tabName, string panelName)
        {
            List<RibbonPanel> list = (from i in uiApp.GetRibbonPanels(tabName)
                                      where i.Name == panelName
                                      select i).ToList<RibbonPanel>();
            bool flag = list.Count == 1;
            RibbonPanel result;
            if (flag)
            {
                result = list.First<RibbonPanel>();
            }
            else
            {
                result = uiApp.CreateRibbonPanel(tabName, panelName);
            }
            return result;
        }
    }
}
