//---------------------------------------------------------------------------
#include <vcl.h>
#pragma hdrstop

#include "CScena.h"
#include "proj.h"
#include "CAniMain.h"
#include "CObjectList.h"
#include "CTools.h"
//---------------------------------------------------------------------------
//#pragma package(smart_init)
#pragma link "CSPIN"
#pragma resource "*.dfm"
TDScena *DScena;
//---------------------------------------------------------------------------
__fastcall TDScena::TDScena(TComponent* Owner)
    : TForm(Owner)
{
}
//---------------------------------------------------------------------------

void __fastcall TDScena::FogColorClick(TObject *Sender)
{
if(ColorDialog1->Execute())
    FogColor->Color = ColorDialog1->Color;
}
//---------------------------------------------------------------------------

void __fastcall TDScena::FormShow(TObject *Sender)
{

if(AniMain->Novy)
	AniMain->temp = new CScena("NovaScena");
else
	AniMain->temp = ((CScena*)AniMain->AktPrvok);

//Nastavenie ovladacov
E->Text=AniMain->temp->GetName();
PX->Text=AniMain->temp->GetX();
PY->Text=AniMain->temp->GetY();
PZ->Text=AniMain->temp->GetZ();
FNear->Text=((CScena*)AniMain->temp)->GetZaciatokHmly();
FFar->Text=((CScena*)AniMain->temp)->GetKoniecHmly();
RX->Text=AniMain->temp->GetAlfa();
RY->Text=AniMain->temp->GetBeta();
RZ->Text=AniMain->temp->GetGama();
FogColor->Color = RGB(255*((CScena*)AniMain->temp)->GetRHmla(),255*((CScena*)AniMain->temp)->GetGHmla(),255*((CScena*)AniMain->temp)->GetBHmla());
T->Text=((CScena*)AniMain->temp)->GetAniTime();

switch (((CScena*)AniMain->temp)->GetTypHmly()) {
case NONE : RBNone->Checked=true;break;
case 1 : RBType1->Checked=true;break;
case 2 : RBType2->Checked=true;break;
case 3 : RBType3->Checked=true;break;
}

Density->Position =((CScena*)AniMain->temp)->GetHustotaHmly()*255;

}
//---------------------------------------------------------------------------
void __fastcall TDScena::BtnOkClick(TObject *Sender)
{
((CScena*)AniMain->temp)->SetAniTime(T->Text.ToDouble());
AniMain->temp->Rename(E->Text.c_str());
AniMain->temp->SetX(PX->Text.ToDouble());
AniMain->temp->SetY(PY->Text.ToDouble());
AniMain->temp->SetZ(PZ->Text.ToDouble());
((CScena*)AniMain->temp)->SetZaciatokHmly(FNear->Text.ToDouble());
((CScena*)AniMain->temp)->SetKoniecHmly(FFar->Text.ToDouble());

if (RBNone->Checked)
    ((CScena*)AniMain->temp)->SetTypHmly(NONE);
if (RBType1->Checked)
    ((CScena*)AniMain->temp)->SetTypHmly(1);
if (RBType2->Checked)
    ((CScena*)AniMain->temp)->SetTypHmly(2);
if (RBType3->Checked)
    ((CScena*)AniMain->temp)->SetTypHmly(3);

AniMain->temp->SetAlfa(RX->Text.ToDouble());
AniMain->temp->SetBeta(RY->Text.ToDouble());
AniMain->temp->SetGama(RZ->Text.ToDouble());
((CScena*)AniMain->temp)->SetRHmla(GetRValue(FogColor->Color)/255.0);
((CScena*)AniMain->temp)->SetGHmla(GetGValue(FogColor->Color)/255.0);
((CScena*)AniMain->temp)->SetBHmla(GetBValue(FogColor->Color)/255.0);
((CScena*)AniMain->temp)->SetHustotaHmly(Density->Position/255.0);

/*
Spec->Color = RGB(255*AniMain->temp->GetRSpec(),255*AniMain->temp->GetGSpec(),255*AniMain->temp->GetBSpec());
Amb->Color = RGB(255*AniMain->temp->GetRAmb(),255*AniMain->temp->GetGAmb(),255*AniMain->temp->GetBAmb());
Dif->Color = RGB(255*AniMain->temp->GetRDif(),255*AniMain->temp->GetGDif(),255*AniMain->temp->GetBDif());
*/

AniMain->Zmena=1;
}
//---------------------------------------------------------------------------
void __fastcall TDScena::BtnCancelClick(TObject *Sender)
{
if (AniMain->Novy) delete AniMain->temp;
}
//---------------------------------------------------------------------------
