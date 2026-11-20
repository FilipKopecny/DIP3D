//---------------------------------------------------------------------------
#include <vcl\vcl.h>
#pragma hdrstop

#include "CKamera.h"
#include "proj.h"
#include "CAniMain.h"
#include "CObjectList.h"
#include "CTools.h"
#include "CGenMovt.h"
//---------------------------------------------------------------------------
#pragma link "CSPIN"
#pragma resource "*.dfm"
TDKamera *DKamera;
//---------------------------------------------------------------------------
__fastcall TDKamera::TDKamera(TComponent* Owner)
	: TForm(Owner)
{
}
//---------------------------------------------------------------------------
//---------------------------------------------------------------------------
void __fastcall TDKamera::BtnOkClick(TObject *Sender)
{
AniMain->temp->SetZaciatokPohybu(ZP->Checked);
AniMain->temp->Rename(E->Text.c_str());
AniMain->temp->SetX(PX->Text.ToDouble());
AniMain->temp->SetY(PY->Text.ToDouble());
AniMain->temp->SetZ(PZ->Text.ToDouble());
AniMain->temp->SetZorUhol(FA->Text.ToDouble());
AniMain->temp->SetAlfa(RX->Text.ToDouble());
AniMain->temp->SetBeta(RY->Text.ToDouble());
AniMain->temp->SetGama(RZ->Text.ToDouble());


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

if (CActual->Checked)
    AniMain->AktScena->SetAktualna(AniMain->temp);


AniMain->Zmena=1;

}
//---------------------------------------------------------------------------
void __fastcall TDKamera::BtnCancelClick(TObject *Sender)
{
if (AniMain->Novy)
     AniMain->AktScena->ZrusKameru(AniMain->temp);
}
//---------------------------------------------------------------------------
void __fastcall TDKamera::FormShow(TObject *Sender)
{

if(AniMain->Novy)
	AniMain->temp = AniMain->AktScena->VytvorNovuKameru("NovaKamera");
else
	AniMain->temp = ((CKamera*)AniMain->AktPrvok);

//Nastavenie ovladacov
ZP->Checked=AniMain->temp->GetZaciatokPohybu();
E->Text=AniMain->temp->GetName();
PX->Text=AniMain->temp->GetX();
PY->Text=AniMain->temp->GetY();
PZ->Text=AniMain->temp->GetZ();
FA->Text=((CKamera*)AniMain->temp)->GetZorUhol();
RX->Text=AniMain->temp->GetAlfa();
RY->Text=AniMain->temp->GetBeta();
RZ->Text=AniMain->temp->GetGama();
CActual->Checked=(((CKamera*)AniMain->temp)->aktualna == 1);
}


void __fastcall TDKamera::GMClick(TObject *Sender)
{
if (AniMain->temp) DGenMovt->ShowModal();
}
//---------------------------------------------------------------------------

