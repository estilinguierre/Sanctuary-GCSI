using System;
using MetalForge.Core.Diagnostics;
using SolidWorks.Interop.sldworks;
using SolidWorks.Interop.swconst;

namespace MetalForge.SwAdapter
{
    /// <summary>
    /// Point d'entrée unique vers SolidWorks. Toutes les cotes reçues sont en mm (Core) ;
    /// la conversion en mètres se fait ici et dans les constructeurs de géométrie, jamais ailleurs.
    /// </summary>
    public sealed class SwSession
    {
        public ISldWorks Sw { get; }

        public SwSession(ISldWorks sw) { Sw = sw ?? throw new ArgumentNullException(nameof(sw)); }

        public string VersionSolidWorks
        {
            get { try { return Sw.RevisionNumber(); } catch { return "?"; } }
        }

        /// <summary>Nouvelle pièce à partir du modèle par défaut de l'utilisateur.</summary>
        public ModelDoc2 NouvellePiece()
        {
            var modele = Sw.GetUserPreferenceStringValue((int)swUserPreferenceStringValue_e.swDefaultTemplatePart);
            return (ModelDoc2)Sw.NewDocument(modele, 0, 0, 0);
        }

        /// <summary>Nouvel assemblage à partir du modèle par défaut de l'utilisateur.</summary>
        public ModelDoc2 NouvelAssemblage()
        {
            var modele = Sw.GetUserPreferenceStringValue((int)swUserPreferenceStringValue_e.swDefaultTemplateAssembly);
            return (ModelDoc2)Sw.NewDocument(modele, 0, 0, 0);
        }

        /// <summary>
        /// Exécute une opération de construction comme une transaction logique (spec § 4 Robustesse) :
        /// en cas d'échec, l'erreur est journalisée et signalée ; dans tous les cas le document est reconstruit (EditRebuild3).
        /// </summary>
        public bool Operation(string nom, ModelDoc2 doc, Action action)
        {
            Journal.Info("Début : " + nom);
            try
            {
                action();
                Journal.Info("Terminé : " + nom);
                return true;
            }
            catch (Exception ex)
            {
                Journal.Erreur("Échec : " + nom, ex);
                Message("MetalForge — " + nom + " : " + ex.Message, swMessageBoxIcon_e.swMbStop);
                return false;
            }
            finally
            {
                try { doc?.EditRebuild3(); } catch (Exception ex) { Journal.Erreur("Reconstruction impossible", ex); }
            }
        }

        public void Message(string texte, swMessageBoxIcon_e icone = swMessageBoxIcon_e.swMbInformation)
        {
            try { Sw.SendMsgToUser2(texte, (int)icone, (int)swMessageBoxBtn_e.swMbOk); } catch { }
        }
    }
}
