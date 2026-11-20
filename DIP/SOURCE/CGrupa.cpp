//---------------------------------------------------------------------------
#include <vcl\vcl.h>
#pragma hdrstop

#include "CGrupa.h"
#include "proj.h"
#include "CAniMain.h"
#include "CObjectList.h"
#include "CTools.h"
#include "CGenMovt.h"
//---------------------------------------------------------------------------
#pragma link "CSPIN"
#pragma resource "*.dfm"
TDGrupa *DGrupa;
//---------------------------------------------------------------------------
__fastcall TDGrupa::TDGrupa(TComponent* Owner)
	: TForm(Owner)
{
}

//---------------------------------------------------------------------------
//---------------------------------------------------------------------------
void __fastcall TDGrupa::FormShow(TObject *Sender)
{

if(AniMain->Novy)
	AniMain->temp = new CGrupa("NovaGrupa");
else
	AniMain->temp = ((CGrupa*)AniMain->AktPrvok);

//Nastavenie ovladacov
ZP->Checked=AniMain->temp->GetZaciatokPohybu();
E->Text=AniMain->temp->GetName();
PX->Text=AniMain->temp->GetX();
PY->Text=AniMain->temp->GetY();
PZ->Text=AniMain->temp->GetZ();
RX->Text=AniMain->temp->GetAlfa();
RY->Text=AniMain->temp->GetBeta();
RZ->Text=AniMain->temp->GetGama();

}
//---------------------------------------------------------------------------
void __fastcall TDGrupa::BtnOkClick(TObject *Sender)
{
AniMain->temp->SetZaciatokPohybu(ZP->Checked);
AniMain->temp->Rename(E->Text.c_str());
AniMain->temp->SetX(PX->Text.ToDouble());
AniMain->temp->SetY(PY->Text.ToDouble());
AniMain->temp->SetZ(PZ->Text.ToDouble());
AniMain->temp->SetAlfa(RX->Text.ToDouble());
AniMain->temp->SetBeta(RY->Text.ToDouble());
AniMain->temp->SetGama(RZ->Text.ToDouble());

/*
Spec->Color = RGB(255*AniMain->temp->GetRSpec(),255*AniMain->temp->GetGSpec(),255*AniMain->temp->GetBSpec());
Amb->Color = RGB(255*AniMain->temp->GetRAmb(),255*AniMain->temp->GetGAmb(),255*AniMain->temp->GetBAmb());
Dif->Color = RGB(255*AniMain->temp->GetRDif(),255*AniMain->temp->GetGDif(),255*AniMain->temp->GetBDif());
*/


if (!AniMain->Novy && ObjectList ) {
    AniMain->AktPrvokNode->Text = E->Text;
    }





 // pridava sa nova grupa
if (AniMain->Novy) {
	if (AniMain->AktPrvok && (
	((CPrvok*)AniMain->AktPrvok)->GetTypPrvku()!=TYP_ANIMACIA &&
	((CPrvok*)AniMain->AktPrvok)->GetTypPrvku()!=TYP_SCENA)) {
	// nieco je SELECTED, a nieJE to ANIMACIA ALEBO SCENA, MOZE SA PRIDAVAT
		((CGrupa*)AniMain->AktPrvokParent)->PridajPrvok(AniMain->temp);
        ((CGrupa*)AniMain->temp)->PridajPrvok(((CGrupa*)AniMain->AktPrvokParent)->VyradPrvok(AniMain->AktPrvok));
		if (ObjectList) {
			ObjectList->T->Items->BeginUpdate();
		 	AniMain->nod=ObjectList->T->Items->AddChildObject(AniMain->AktPrvokParentNode,AniMain->temp->GetName(),AniMain->temp);
            ObjectList->T->Items->AddChildObject(AniMain->nod,AniMain->AktPrvok->GetName(),AniMain->AktPrvok);
			AniMain->AktPrvokNode->Delete();
		   	ObjectList->T->Items->EndUpdate();
            AniMain->SetAktPrvokNodeIfPossible(); // cause of delete 2 lines up 
        }
	}
}
AniMain->Zmena=1;
}
//---------------------------------------------------------------------------
void __fastcall TDGrupa::BtnCancelClick(TObject *Sender)
{
if (AniMain->Novy) delete AniMain->temp;
}
//---------------------------------------------------------------------------




void __fastcall TDGrupa::GMClick(TObject *Sender)
{
if (AniMain->temp) DGenMovt->ShowModal();    
}
//---------------------------------------------------------------------------

