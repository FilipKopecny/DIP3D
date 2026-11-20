//---------------------------------------------------------------------------
#include <vcl\vcl.h>
#pragma hdrstop

#include "CGenSettings.h"
#include "CAniMain.h"
//---------------------------------------------------------------------------
#pragma resource "*.dfm"

TGenSettings *GenSettings;
//---------------------------------------------------------------------------
__fastcall TGenSettings::TGenSettings(TComponent* Owner)
	: TForm(Owner)
{
}
//---------------------------------------------------------------------------
//---------------------------------------------------------------------------
void __fastcall TGenSettings::FormShow(TObject *Sender)
{
RB1024->Checked = false;
RB800->Checked = false;
RB640->Checked = false;
RB320->Checked = false;
if (AniMain) {
switch (AniMain->ClientWidth) {
	case 1024 : RB1024->Checked = true;break;
	case 800 : RB800->Checked = true;break;
	case 640 : RB640->Checked = true;break;
	case 320 : RB320->Checked = true;break;
}

if (AniMain->anti)
    AA->Checked=true;
else
    AA->Checked=false;

if (AniMain->alfa)
    AB->Checked=true;
else
    AB->Checked=false;


}
}
//---------------------------------------------------------------------------
//-----------------------------------------------------------------------
void __fastcall TGenSettings::BtnOkClick(TObject *Sender)
{
if (GenSettings->RB1024->Checked) {
	AniMain->ClientWidth = 1024;
	AniMain->ClientHeight = 768;
	//break;
	//return;
}

if (GenSettings->RB800->Checked) {
	AniMain->ClientWidth = 800;
	AniMain->ClientHeight = 600;
    //break;
    //return;
}

if (GenSettings->RB640->Checked) {
	AniMain->ClientWidth = 640;
	AniMain->ClientHeight = 480;
	//break;
	//return
}

if (GenSettings->RB320->Checked) {
	AniMain->ClientWidth = 320;
	AniMain->ClientHeight = 200;
	//break;
    //return;
}


if (AB->Checked) {
    glEnable (GL_BLEND);
    glBlendFunc( GL_SRC_ALPHA,GL_ONE_MINUS_SRC_ALPHA);
    glEnable(GL_ALPHA_TEST);
    glAlphaFunc(GL_GREATER,0.004);
    AniMain->alfa=1;
    }
    else {
    glDisable (GL_BLEND);
    glDisable(GL_ALPHA_TEST);
    AniMain->alfa=0;
    }

if (AA->Checked) {
    glEnable (GL_LINE_SMOOTH);
    glHint (GL_LINE_SMOOTH_HINT, GL_NICEST);
    glEnable (GL_POLYGON_SMOOTH);
    glHint (GL_POLYGON_SMOOTH_HINT, GL_NICEST);
    AniMain->anti=1;
    }
    else {
    glDisable (GL_LINE_SMOOTH);
    glDisable (GL_POLYGON_SMOOTH);
    AniMain->anti=0;
    }

AniMain->Zmena=1;
}
//---------------------------------------------------------------------------
