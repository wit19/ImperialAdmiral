public class Cell { public Cell(int a, int b) {} }

// Player-25 M14b-C1 (DeploymentFilterGate): the gate compiles the REAL
// Saga/MissionModels/DeploymentPointProps.cs. Its card/enum references get
// compile-time stubs - the card stub lives in namespace Saga (never global)
// so it resolves the real props file's unqualified `DeploymentCard` while
// the gate's own files keep their using-imported TacticalAI.Core types
// (shadowing law: a global type would beat the usings, skill
// unity-headless-compile). Only the four fields IsFilteredOut reads exist.
public enum FigureSize { Small1x1, Medium1x2, Large2x2, Huge2x3 }

namespace Saga
{
    public class DeploymentCard
    {
        public string faction;
        public FigureSize miniSize;
        public string[] traits;
        public string[] traitsEn;   // the M14b-C1 EN-cache lane
    }
}
