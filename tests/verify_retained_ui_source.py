from pathlib import Path

ROOT = Path(__file__).resolve().parents[1]
SRC = ROOT / "src"

def read(name):
    return (SRC / name).read_text(encoding="utf-8")

def require(cond, msg):
    if not cond:
        raise AssertionError(msg)

panel = read("PvpPanel.cs")
plugin = read("ErenshorPvPPlugin.cs")
controller = read("PvpController.cs")
drag = read("PvpDragGuard.cs")
aura = read("PvpSuiteAuraProvider.cs")
control = read("PvpControlApi.cs")
project = (ROOT / "ErenshorPvP.csproj").read_text(encoding="utf-8")

for token in ("OnGUI", "GUILayout", "GUI.Window", "GUI.DragWindow", "GameData.EditUIMode", "PlayerControl.LeftClick", "csMouseOrbit"):
    require(token not in panel + plugin, "forbidden production UI token: " + token)

for token in ("CanvasScaler", "GraphicRaycaster", "TextMeshProUGUI", "ScrollRect", "PvpDragGuard"):
    require(token in panel, "retained UI component missing: " + token)
require("GameData.DraggingUIElement = true" in drag and "GameData.DraggingUIElement = _nativeFlagBeforeFirstOwner" in drag, "drag ownership does not restore prior native state")
require("headerGripRaycast" in panel and "new Color(0f, 0f, 0f, 0f)" in panel, "panel drag surface must be raycastable")
require("PvpControlApi." in panel, "panel callbacks do not route through ControlApi")
for forbidden_mutation in ("SpawnVisualClone(", "BeginTargetingTest(", "BeginLethalFight(", "Despawn(\"manual\")"):
    require(forbidden_mutation not in panel, "production panel exposes debug combat mutation: " + forbidden_mutation)
require("showLauncher" in aura and "openPanel" in aura and "resetLauncher" in aura, "Aura panel/launcher contract incomplete")
require("return PvpController.HubStatus();" in control, "ControlApi does not use concise Hub status")
require("RequestOpenPanel" in controller and "RequestClosePanel" in controller, "retained UI open/close control contract missing")
require("Unity.TextMeshPro" in project and "UnityEngine.IMGUIModule" not in project, "project references do not match retained UI stack")
require("BepInEx.dll" not in project and 'Reference Include="BepInEx' not in project, "BepInEx project reference remains")
print("verify_retained_ui_source: PASS")
