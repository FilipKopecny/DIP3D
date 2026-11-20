//---------------------------------------------------------------------------
#include <vcl\vcl.h>
#include <stdlib.h>
#pragma hdrstop

#include "CAniSettings.h"
#include "CAniMain.h"
//---------------------------------------------------------------------------
#pragma resource "*.dfm"
TAniSettings *AniSettings;
//---------------------------------------------------------------------------
__fastcall TAniSettings::TAniSettings(TComponent* Owner)
	: TForm(Owner)
{
}
//---------------------------------------------------------------------------
void __fastcall TAniSettings::FormShow(TObject *Sender)
{
 if (AniMain->Animacia) {
 FPS->Text = AniMain->Animacia->GetFPS();
 UDFps->Max = 128;
 UDFps->Min = 1;
 UDFps->Position = FPS->Text.ToInt();
 }
}
//---------------------------------------------------------------------------
void __fastcall TAniSettings::UDFpsClick(TObject *Sender, TUDBtnType Button)
{

if (Button==0)
	FPS->Text = atoi(FPS->Text.c_str())+1;
else
	FPS->Text = atoi(FPS->Text.c_str())-1;

}
//---------------------------------------------------------------------------
void __fastcall TAniSettings::FPSChange(TObject *Sender)
{
if (atoi(FPS->Text.c_str()) > 128) FPS->Text = 128;
if (atoi(FPS->Text.c_str()) < 1) FPS->Text = 1;
UDFps->Position = atoi(FPS->Text.c_str());
}
//---------------------------------------------------------------------------
void __fastcall TAniSettings::BtnOkClick(TObject *Sender)
{
if (AniMain->Animacia)
    AniMain->Animacia->SetFPS(FPS->Text.ToInt());
}
//---------------------------------------------------------------------------

