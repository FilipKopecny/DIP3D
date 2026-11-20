//---------------------------------------------------------------------------
#include <vcl\vcl.h>
#pragma hdrstop

#include "CLight.h"
#include "proj.h"
#include "CAniMain.h"
#include "CObjectList.h"
#include "CTools.h"
#include "CGenMovt.h"
//---------------------------------------------------------------------------
#pragma link "CSPIN"
#pragma resource "*.dfm"
TDSvetlo *DSvetlo;
//---------------------------------------------------------------------------
__fastcall TDSvetlo::TDSvetlo(TComponent* Owner)
	: TForm(Owner)
{
}
void __fastcall TDSvetlo::SpecClick(TObject *Sender)
{
if (ColorDialog1->Execute())
Spec->Color=ColorDialog1->Color;
}
//---------------------------------------------------------------------------
void __fastcall TDSvetlo::AmbClick(TObject *Sender)
{
if (ColorDialog1->Execute())
Amb->Color=ColorDialog1->Color;
}
//---------------------------------------------------------------------------
void __fastcall TDSvetlo::DifClick(TObject *Sender)
{
if (ColorDialog1->Execute())
Dif->Color=ColorDialog1->Color;
}
//---------------------------------------------------------------------------
void __fastcall TDSvetlo::EmiClick(TObject *Sender)
{
if (ColorDialog1->Execute())
Emi->Color=ColorDialog1->Color;
}
//---------------------------------------------------------------------------

void __fastcall TDSvetlo::FormShow(TObject *Sender)
{

if(AniMain->Novy)
	AniMain->temp = ((CSvetlo*)AniMain->AktScena->VytvorNoveSvetlo("NoveSvetlo"));
else
	AniMain->temp = ((CSvetlo*)AniMain->AktPrvok);

//Nastavenie ovladacov


ZP->Checked=AniMain->temp->GetZaciatokPohybu();
E->Text=AniMain->temp->GetName();
PX->Text=AniMain->temp->GetX();
PY->Text=AniMain->temp->GetY();
PZ->Text=AniMain->temp->GetZ();
// SE je naopak
SE->Position=129-((double)((CSvetlo*)AniMain->temp)->GetSpotExponent());
SC->Position=((double)((CSvetlo*)AniMain->temp)->GetSpotCutoff());

CEnable->Checked=((CSvetlo*)AniMain->temp)->GetLightState();

TC->Position=((double)((CSvetlo*)AniMain->temp)->GetConstant())*100;
TL->Position=((double)((CSvetlo*)AniMain->temp)->GetLinear())*100;
TQ->Position=((double)((CSvetlo*)AniMain->temp)->GetQuadratic())*100;

RX->Text=AniMain->temp->GetAlfa();
RY->Text=AniMain->temp->GetBeta();
RZ->Text=AniMain->temp->GetGama();
Spec->Color = RGB(255*AniMain->temp->GetRSpec(),255*AniMain->temp->GetGSpec(),255*AniMain->temp->GetBSpec());
Amb->Color = RGB(255*AniMain->temp->GetRAmb(),255*AniMain->temp->GetGAmb(),255*AniMain->temp->GetBAmb());
Dif->Color = RGB(255*AniMain->temp->GetRDif(),255*AniMain->temp->GetGDif(),255*AniMain->temp->GetBDif());
Emi->Color = RGB(255*AniMain->temp->GetREmis(),255*AniMain->temp->GetGEmis(),255*AniMain->temp->GetBEmis());


if (((CSvetlo*)AniMain->temp)->GetSpotCutoff() > 178.0) {
    ((CSvetlo*)AniMain->temp)->SetSpotCutoff(180.0);
    RGO->Checked=true;
    }
else
    {
    RGS->Checked=true;
    }





}
//---------------------------------------------------------------------------
void __fastcall TDSvetlo::BtnOkClick(TObject *Sender)
{
AniMain->temp->SetZaciatokPohybu(ZP->Checked);
AniMain->temp->Rename(E->Text.c_str());
AniMain->temp->SetX(PX->Text.ToDouble());
AniMain->temp->SetY(PY->Text.ToDouble());
AniMain->temp->SetZ(PZ->Text.ToDouble());
AniMain->temp->SetAlfa(RX->Text.ToDouble());
AniMain->temp->SetBeta(RY->Text.ToDouble());
AniMain->temp->SetGama(RZ->Text.ToDouble());
((CSvetlo*)AniMain->temp)->SetSpotExponent(129-((double)SE->Position));
if (RGS->Checked)
    ((CSvetlo*)AniMain->temp)->SetSpotCutoff(SC->Position);
else
    ((CSvetlo*)AniMain->temp)->SetSpotCutoff(180.0);

((CSvetlo*)AniMain->temp)->SetConstant(TC->Position/100.0);
((CSvetlo*)AniMain->temp)->SetLinear(TL->Position/100.0);
((CSvetlo*)AniMain->temp)->SetQuadratic(TQ->Position/100.0);

((CSvetlo*)AniMain->temp)->SetLightState(CEnable->Checked);


AniMain->temp->SetAlfa(RX->Text.ToDouble());
AniMain->temp->SetBeta(RY->Text.ToDouble());
AniMain->temp->SetGama(RZ->Text.ToDouble());
AniMain->temp->SetRAmb(GetRValue(Amb->Color)/255.0);
AniMain->temp->SetGAmb(GetGValue(Amb->Color)/255.0);
AniMain->temp->SetBAmb(GetBValue(Amb->Color)/255.0);
AniMain->temp->SetRDif(GetRValue(Dif->Color)/255.0);
AniMain->temp->SetGDif(GetGValue(Dif->Color)/255.0);
AniMain->temp->SetBDif(GetBValue(Dif->Color)/255.0);
AniMain->temp->SetREmis(GetRValue(Emi->Color)/255.0);
AniMain->temp->SetGEmis(GetGValue(Emi->Color)/255.0);
AniMain->temp->SetBEmis(GetBValue(Emi->Color)/255.0);
AniMain->temp->SetRSpec(GetRValue(Spec->Color)/255.0);
AniMain->temp->SetGSpec(GetGValue(Spec->Color)/255.0);
AniMain->temp->SetBSpec(GetBValue(Spec->Color)/255.0);


/*
Spec->Color = RGB(255*AniMain->temp->GetRSpec(),255*AniMain->temp->GetGSpec(),255*AniMain->temp->GetBSpec());
Amb->Color = RGB(255*AniMain->temp->GetRAmb(),255*AniMain->temp->GetGAmb(),255*AniMain->temp->GetBAmb());
Dif->Color = RGB(255*AniMain->temp->GetRDif(),255*AniMain->temp->GetGDif(),255*AniMain->temp->GetBDif());
*/


if (!AniMain->Novy && ObjectList ) {
    AniMain->AktPrvokNode->Text = E->Text;
    }

if (AniMain->Novy) {
	if (AniMain->AktPrvok && (
	((CPrvok*)AniMain->AktPrvok)->GetTypPrvku()==TYP_GRUPA ||
	((CPrvok*)AniMain->AktPrvok)->GetTypPrvku()==TYP_SCENA)) {
	// nieco je SELECTED, a JE to GRUPA ALEBO SCENA, MOZE SA PRIDAVAT
		((CGrupa*)AniMain->AktPrvok)->PridajPrvok(AniMain->temp);
		if (ObjectList) {
			ObjectList->T->Items->BeginUpdate();
		 	AniMain->nod=ObjectList->T->Items->AddChildObject(AniMain->AktPrvokNode,AniMain->temp->GetName(),AniMain->temp);
		 	ObjectList->T->Items->EndUpdate();
	    }
	}
	else {
		((CGrupa*)AniMain->AktScena)->PridajPrvok(AniMain->temp);
	    if (ObjectList) {
			ObjectList->T->Items->BeginUpdate();
			AniMain->nod=ObjectList->T->Items->AddChildObject(AniMain->AktScenaNode,AniMain->temp->GetName(),AniMain->temp);
			ObjectList->T->Items->EndUpdate();
		}
	}
}

AniMain->Zmena=1;
}
//---------------------------------------------------------------------------
void __fastcall TDSvetlo::BtnCancelClick(TObject *Sender)
{
if (AniMain->Novy)
     AniMain->AktScena->ZrusSvetlo(((CSvetlo*)AniMain->temp));
}
//---------------------------------------------------------------------------//---------------------------------------------------------------------------

void __fastcall TDSvetlo::GMClick(TObject *Sender)
{
if (AniMain->temp) DGenMovt->ShowModal();    
}
//---------------------------------------------------------------------------

