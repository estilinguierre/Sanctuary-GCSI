// Maquette minimale de l'API SolidWorks (mêmes noms et signatures que les interops officielles),
// limitée aux membres utilisés par MetalForge. Compléter au fil des lots.
using System;

namespace SolidWorks.Interop.swpublished
{
    public interface ISwAddin { bool ConnectToSW(object ThisSW, int Cookie); bool DisconnectFromSW(); }
}

namespace SolidWorks.Interop.swconst
{
    public enum swDocumentTypes_e { swDocNONE = 0, swDocPART = 1, swDocASSEMBLY = 2, swDocDRAWING = 3 }
    public enum swUserPreferenceStringValue_e { swDefaultTemplatePart = 8, swDefaultTemplateAssembly = 9, swDefaultTemplateDrawing = 10 }
    public enum swMessageBoxIcon_e { swMbWarning = 1, swMbInformation = 2, swMbQuestion = 3, swMbStop = 4 }
    public enum swMessageBoxBtn_e { swMbAbortRetryIgnore = 1, swMbOk = 2, swMbOkCancel = 3 }
}

namespace SolidWorks.Interop.sldworks
{
    public interface ISldWorks
    {
        bool SetAddinCallbackInfo2(int Instance, object CallBack, int Cookie);
        TaskpaneView CreateTaskpaneView2(string Bitmap, string ToolTip);
        string GetUserPreferenceStringValue(int UserPreferenceValue);
        object NewDocument(string TemplateName, int PaperSize, double Width, double Height);
        object ActiveDoc { get; }
        string RevisionNumber();
        int SendMsgToUser2(string Message, int Icon, int Buttons);
    }
    public interface SldWorks : ISldWorks { }
    public interface ITaskpaneView { bool DisplayWindowFromHandlex64(long Handle); bool DeleteView(); }
    public interface TaskpaneView : ITaskpaneView { }
    public interface IModelDoc2 { string GetTitle(); bool EditRebuild3(); bool ForceRebuild3(bool TopOnly); void ClearSelection2(bool All); }
    public interface ModelDoc2 : IModelDoc2 { }
}
