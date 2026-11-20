//---------------------------------------------------------------------------
#include <vcl\vcl.h>
#pragma hdrstop

#include "CMesh.h"
#include "proj.h"
#include "CAniMain.h"
#include "CObjectList.h"
#include "CTools.h"
#include "CGenMovt.h"
//---------------------------------------------------------------------------
#pragma link "CSPIN"
#pragma resource "*.dfm"
TDMesh *DMesh;
//---------------------------------------------------------------------------
__fastcall TDMesh::TDMesh(TComponent* Owner)
	: TForm(Owner)
{
}
//---------------------------------------------------------------------------
void __fastcall TDMesh::SpecClick(TObject *Sender)
{
if (ColorDialog1->Execute())
Spec->Color=ColorDialog1->Color;
}
//---------------------------------------------------------------------------
void __fastcall TDMesh::AmbClick(TObject *Sender)
{
if (ColorDialog1->Execute())
Amb->Color=ColorDialog1->Color;
}
//---------------------------------------------------------------------------
void __fastcall TDMesh::DifClick(TObject *Sender)
{
if (ColorDialog1->Execute())
Dif->Color=ColorDialog1->Color;
}
//---------------------------------------------------------------------------
void __fastcall TDMesh::EmiClick(TObject *Sender)
{
if (ColorDialog1->Execute())
Emi->Color=ColorDialog1->Color;
}
//---------------------------------------------------------------------------
void __fastcall TDMesh::Button1Click(TObject *Sender)
{
if(DOpen->Execute()) {
      AniMain->AktPrvok->LoadTexture(DOpen->FileName.c_str());
}
}
//---------------------------------------------------------------------------
//---------------------------------------------------------------------------
void __fastcall TDMesh::FormShow(TObject *Sender)
{

if(AniMain->Novy)
	AniMain->temp = new CMesh("NovyMesh");
else
	AniMain->temp = ((CMesh*)AniMain->AktPrvok);

//Nastavenie ovladacov
E->Text=AniMain->temp->GetName();
PX->Text=AniMain->temp->GetX();
PY->Text=AniMain->temp->GetY();
PZ->Text=AniMain->temp->GetZ();
FX->Text=((double)((CMesh*)AniMain->temp)->GetQualityX());
FY->Text=((double)((CMesh*)AniMain->temp)->GetQualityY());
FR->Text=((CMesh*)AniMain->temp)->GetPolomer();
RX->Text=AniMain->temp->GetAlfa();
RY->Text=AniMain->temp->GetBeta();
RZ->Text=AniMain->temp->GetGama();
Spec->Color = RGB(255*AniMain->temp->GetRSpec(),255*AniMain->temp->GetGSpec(),255*AniMain->temp->GetBSpec());
Amb->Color = RGB(255*AniMain->temp->GetRAmb(),255*AniMain->temp->GetGAmb(),255*AniMain->temp->GetBAmb());
Dif->Color = RGB(255*AniMain->temp->GetRDif(),255*AniMain->temp->GetGDif(),255*AniMain->temp->GetBDif());
Emi->Color = RGB(255*AniMain->temp->GetREmis(),255*AniMain->temp->GetGEmis(),255*AniMain->temp->GetBEmis());
Shininess->Position=AniMain->temp->GetShine()*255.0;
AS->Text=(int)(AniMain->temp->GetASpec()*255.0);
AA->Text=(int)(AniMain->temp->GetAAmb()*255.0);
AD->Text=(int)(AniMain->temp->GetADif()*255.0);
AE->Text=(int)(AniMain->temp->GetAEmis()*255.0);
ZP->Checked=AniMain->temp->GetZaciatokPohybu();
FR->Value=1.0;
}
//---------------------------------------------------------------------------
void __fastcall TDMesh::BtnOkClick(TObject *Sender)
{
AniMain->temp->SetZaciatokPohybu(ZP->Checked);
AniMain->temp->SetASpec(AS->Text.ToDouble()/255.0);
AniMain->temp->SetAAmb(AA->Text.ToDouble()/255.0);
AniMain->temp->SetADif(AD->Text.ToDouble()/255.0);
AniMain->temp->SetAEmis(AE->Text.ToDouble()/255.0);
AniMain->temp->SetShine(Shininess->Position/255.0);
AniMain->temp->Rename(E->Text.c_str());
AniMain->temp->SetX(PX->Text.ToDouble());
AniMain->temp->SetY(PY->Text.ToDouble());
AniMain->temp->SetZ(PZ->Text.ToDouble());
AniMain->temp->SetQualityX(FX->Text.ToDouble());
AniMain->temp->SetQualityY(FY->Text.ToDouble());
AniMain->temp->SetPolomer(FR->Text.ToDouble());
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
AniMain->temp->Resize(FR->Value);
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
void __fastcall TDMesh::BtnCancelClick(TObject *Sender)
{
if (AniMain->Novy) delete AniMain->temp;
}
//---------------------------------------------------------------------------


void __fastcall TDMesh::NTClick(TObject *Sender)
{
AniMain->temp->texWidth = AniMain->temp->texHeight = 0;
if (AniMain->temp->textureImage)
		delete AniMain->temp->textureImage;
AniMain->temp->textureImage = NULL;

}
//---------------------------------------------------------------------------

void __fastcall TDMesh::Image4Click(TObject *Sender)
{
((CMesh*)AniMain->temp)->SetMesh(&mesh4Points[0][0][0],mesh4DimX,mesh4DimY);//  //
}
//---------------------------------------------------------------------------

void __fastcall TDMesh::Image3Click(TObject *Sender)
{
((CMesh*)AniMain->temp)->SetMesh(&mesh3Points[0][0][0],mesh3DimX,mesh3DimY);//  //
}
//---------------------------------------------------------------------------

void __fastcall TDMesh::Image2Click(TObject *Sender)
{
((CMesh*)AniMain->temp)->SetMesh(&mesh2Points[0][0][0],mesh2DimX,mesh2DimY);//  //
}
//---------------------------------------------------------------------------

void __fastcall TDMesh::Image1Click(TObject *Sender)
{
   ((CMesh*)AniMain->temp)->SetMesh(&mesh1Points[0][0][0],mesh1DimX,mesh1DimY);//
}
//---------------------------------------------------------------------------

void __fastcall TDMesh::GMClick(TObject *Sender)
{
if (AniMain->temp) DGenMovt->ShowModal();    
}
//---------------------------------------------------------------------------

