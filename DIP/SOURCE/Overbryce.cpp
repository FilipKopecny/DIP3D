//---------------------------------------------------------------------------
#include <vcl.h>
#pragma hdrstop
USERES("Overbryce.res");
USEUNIT("trojuhol.cpp");
USEFORM("CAbout.cpp", About);
USEFORM("CAniMain.cpp", AniMain);
USEFORM("CAniSettings.cpp", AniSettings);
USEFORM("CDisk.cpp", DDisk);
USEFORM("CGenMovt.cpp", DGenMovt);
USEFORM("CGenSettings.cpp", GenSettings);
USEFORM("CGrupa.cpp", DGrupa);
USEFORM("CGula.cpp", DGula);
USEFORM("CKamera.cpp", DKamera);
USEFORM("CKuzel.cpp", DKuzel);
USEFORM("CKvader.cpp", DKvader);
USEFORM("CLight.cpp", DSvetlo);
USEFORM("CMesh.cpp", DMesh);
USEFORM("CObdlznik.cpp", DObdlznik);
USEFORM("CObjectList.cpp", ObjectList);
USEFORM("CScena.cpp", DScena);
USEFORM("CTools.cpp", Tools);
USEFORM("CToroid.cpp", DToroid);
USEFORM("CTrojuhol.cpp", DTrojuholnik);
USEUNIT("grupa.cpp");
USEUNIT("kamera.cpp");
USEUNIT("kvader.cpp");
USEUNIT("kvadriky.cpp");
USEUNIT("mesh.cpp");
USEUNIT("obdlznik.cpp");
USEUNIT("proj.cpp");
USEUNIT("prvok.cpp");
USEUNIT("scena.cpp");
USEUNIT("svetlo.cpp");
USEUNIT("torus.cpp");
USEUNIT("animacia.cpp");
//---------------------------------------------------------------------------
WINAPI WinMain(HINSTANCE, HINSTANCE, LPSTR, int)
{
    try
    {
        Application->Initialize();
        Application->CreateForm(__classid(TAniMain), &AniMain);
        Application->CreateForm(__classid(TAbout), &About);
        Application->CreateForm(__classid(TAniSettings), &AniSettings);
        Application->CreateForm(__classid(TDDisk), &DDisk);
        Application->CreateForm(__classid(TDGenMovt), &DGenMovt);
        Application->CreateForm(__classid(TGenSettings), &GenSettings);
        Application->CreateForm(__classid(TDGrupa), &DGrupa);
        Application->CreateForm(__classid(TDGula), &DGula);
        Application->CreateForm(__classid(TDKamera), &DKamera);
        Application->CreateForm(__classid(TDKuzel), &DKuzel);
        Application->CreateForm(__classid(TDKvader), &DKvader);
        Application->CreateForm(__classid(TDSvetlo), &DSvetlo);
        Application->CreateForm(__classid(TDMesh), &DMesh);
        Application->CreateForm(__classid(TDObdlznik), &DObdlznik);
        Application->CreateForm(__classid(TObjectList), &ObjectList);
        Application->CreateForm(__classid(TDScena), &DScena);
        Application->CreateForm(__classid(TTools), &Tools);
        Application->CreateForm(__classid(TDToroid), &DToroid);
        Application->CreateForm(__classid(TDTrojuholnik), &DTrojuholnik);
        Application->Run();
    }
    catch (Exception &exception)
    {
        Application->ShowException(&exception);
    }
    return 0;
}
//---------------------------------------------------------------------------
