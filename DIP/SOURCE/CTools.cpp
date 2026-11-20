//---------------------------------------------------------------------------
#include <vcl\vcl.h>
#pragma hdrstop

#include "CTools.h"
#include "CGula.h"
#include "CLight.h"
#include "CToroid.h"
#include "CKvader.h"
#include "CMesh.h"
#include "CKuzel.h"
#include "CTrojuhol.h"
#include "CKamera.h"
#include "CDisk.h"
#include "CObdlznik.h"
#include "CAniMain.h"
#include "CObjectList.h"
//---------------------------------------------------------------------------
#pragma link "CSPIN"
#pragma resource "*.dfm"
TTools *Tools;
//---------------------------------------------------------------------------
__fastcall TTools::TTools(TComponent* Owner)
	: TForm(Owner)
{
}
//---------------------------------------------------------------------------
//---------------------------------------------------------------------------
void __fastcall TTools::GulaClick(TObject *Sender)
{
if (!AniMain->Animacia)
	MessageBox (NULL,"Create an Animation first !","Fatal",MB_OK);
else   {
    AniMain->Novy=1;
    DGula->ShowModal();
    }
}
//---------------------------------------------------------------------------
void __fastcall TTools::TrojuholClick(TObject *Sender)
{
if (!AniMain->Animacia)
	MessageBox (NULL,"Create an Animation first !","Fatal",MB_OK);
else {
    AniMain->Novy=1;
	DTrojuholnik->ShowModal();
    }
}
//---------------------------------------------------------------------------
void __fastcall TTools::ObdlznikClick(TObject *Sender)
{
if (!AniMain->Animacia)
	MessageBox (NULL,"Create an Animation first !","Fatal",MB_OK);
else {
	AniMain->Novy=1;
	DObdlznik->ShowModal();
	}
}
//---------------------------------------------------------------------------
void __fastcall TTools::DiskClick(TObject *Sender)
{
if (!AniMain->Animacia)
	MessageBox (NULL,"Create an Animation first !","Fatal",MB_OK);
else     {
    AniMain->Novy=1;
	DDisk->ShowModal();
	}
}
//---------------------------------------------------------------------------
void __fastcall TTools::KvaderClick(TObject *Sender)
{
if (!AniMain->Animacia)
	MessageBox (NULL,"Create an Animation first !","Fatal",MB_OK);
else {
	AniMain->Novy=1;
	DKvader->ShowModal();
    }
}
//---------------------------------------------------------------------------
void __fastcall TTools::KameraClick(TObject *Sender)
{
if (!AniMain->Animacia)
	MessageBox (NULL,"Create an Animation first !","Fatal",MB_OK);
else	{
	AniMain->Novy=1;
	DKamera->ShowModal();
	}
}
//---------------------------------------------------------------------------
void __fastcall TTools::ZkuzelClick(TObject *Sender)
{
if (!AniMain->Animacia)
	MessageBox (NULL,"Create an Animation first !","Fatal",MB_OK);
else {
	AniMain->Novy=1;
DKuzel->ShowModal();
}
}
//---------------------------------------------------------------------------
void __fastcall TTools::ToroidClick(TObject *Sender)
{
if (!AniMain->Animacia)
	MessageBox (NULL,"Create an Animation first !","Fatal",MB_OK);
else {
	AniMain->Novy=1;
DToroid->ShowModal();
}
}
//---------------------------------------------------------------------------
void __fastcall TTools::MeshClick(TObject *Sender)
{
if (!AniMain->Animacia)
	MessageBox (NULL,"Create an Animation first !","Fatal",MB_OK);
else {
	AniMain->Novy=1;
DMesh->ShowModal();
}
}
//---------------------------------------------------------------------------
void __fastcall TTools::SvetloClick(TObject *Sender)
{
if (!AniMain->Animacia)
	MessageBox (NULL,"Create an Animation first !","Fatal",MB_OK);
else {
	AniMain->Novy=1;
DSvetlo->ShowModal();
}
}
//---------------------------------------------------------------------------

void __fastcall TTools::XMRMouseDown(TObject *Sender, TMouseButton Button,
	TShiftState Shift, int X, int Y)
{
  if (AniMain->AktPrvok)
	if (AniMain->AktPrvok!=AniMain->Animacia)
    	AniMain->AktPrvok->SetX((AniMain->AktPrvok->GetX())+M->Value);
AniMain->Zmena=1;
}
//---------------------------------------------------------------------------
void __fastcall TTools::XMLMouseDown(TObject *Sender, TMouseButton Button,
	TShiftState Shift, int X, int Y)
{
  if (AniMain->AktPrvok)
	if (AniMain->AktPrvok!=AniMain->Animacia)
    	AniMain->AktPrvok->SetX(AniMain->AktPrvok->GetX()-M->Value);
AniMain->Zmena=1;
XML->Invalidate();
}
//---------------------------------------------------------------------------
void __fastcall TTools::ZMZMouseDown(TObject *Sender, TMouseButton Button,
	TShiftState Shift, int X, int Y)
{
 if (AniMain->AktPrvok)
	if (AniMain->AktPrvok!=AniMain->Animacia)
    	AniMain->AktPrvok->SetZ(AniMain->AktPrvok->GetZ()-M->Value);
AniMain->Zmena=1;

}
//---------------------------------------------------------------------------

void __fastcall TTools::YMHMouseDown(TObject *Sender, TMouseButton Button,
	TShiftState Shift, int X, int Y)
{
 if (AniMain->AktPrvok)
	if (AniMain->AktPrvok!=AniMain->Animacia)
    	AniMain->AktPrvok->SetY(AniMain->AktPrvok->GetY()+M->Value);
                 AniMain->Zmena=1;
}
//---------------------------------------------------------------------------
void __fastcall TTools::YMDMouseDown(TObject *Sender, TMouseButton Button,
	TShiftState Shift, int X, int Y)
{
 if (AniMain->AktPrvok)
	if (AniMain->AktPrvok!=AniMain->Animacia)
    	AniMain->AktPrvok->SetY(AniMain->AktPrvok->GetY()-M->Value);
AniMain->Zmena=1;
}
//---------------------------------------------------------------------------
void __fastcall TTools::ZMPMouseDown(TObject *Sender, TMouseButton Button,
	TShiftState Shift, int X, int Y)
{
 if (AniMain->AktPrvok)
	if (AniMain->AktPrvok!=AniMain->Animacia)
    	AniMain->AktPrvok->SetZ(AniMain->AktPrvok->GetZ()+M->Value);

AniMain->Zmena=1;
}
//---------------------------------------------------------------------------
void __fastcall TTools::YRCWMouseDown(TObject *Sender, TMouseButton Button,
	TShiftState Shift, int X, int Y)
{
  if (AniMain->AktPrvok)
	if (AniMain->AktPrvok!=AniMain->Animacia)
    	AniMain->AktPrvok->SetBeta(AniMain->AktPrvok->GetBeta()-R->Value);

AniMain->Zmena=1;
}
//---------------------------------------------------------------------------
void __fastcall TTools::XRCWMouseDown(TObject *Sender, TMouseButton Button,
	TShiftState Shift, int X, int Y)
{
  if (AniMain->AktPrvok)
	if (AniMain->AktPrvok!=AniMain->Animacia)
    	AniMain->AktPrvok->SetAlfa(AniMain->AktPrvok->GetAlfa()-R->Value);
AniMain->Zmena=1;
}
//---------------------------------------------------------------------------
void __fastcall TTools::ZRCCWMouseDown(TObject *Sender, TMouseButton Button,
	TShiftState Shift, int X, int Y)
{
  if (AniMain->AktPrvok)
	if (AniMain->AktPrvok!=AniMain->Animacia)
    	AniMain->AktPrvok->SetGama(AniMain->AktPrvok->GetGama()+R->Value);
AniMain->Zmena=1;
}
//---------------------------------------------------------------------------
void __fastcall TTools::ZRCWMouseDown(TObject *Sender, TMouseButton Button,
	TShiftState Shift, int X, int Y)
{
  if (AniMain->AktPrvok)
	if (AniMain->AktPrvok!=AniMain->Animacia)
    	AniMain->AktPrvok->SetGama(AniMain->AktPrvok->GetGama()-R->Value);
AniMain->Zmena=1;
}
//---------------------------------------------------------------------------
  void __fastcall TTools::YRCCWMouseDown(TObject *Sender, TMouseButton Button,
	TShiftState Shift, int X, int Y)
{
 if (AniMain->AktPrvok)
	if (AniMain->AktPrvok!=AniMain->Animacia)
    	AniMain->AktPrvok->SetBeta(AniMain->AktPrvok->GetBeta()+R->Value);
AniMain->Zmena=1;
}
//---------------------------------------------------------------------------
void __fastcall TTools::XRCCWMouseDown(TObject *Sender, TMouseButton Button,
	TShiftState Shift, int X, int Y)
{
  if (AniMain->AktPrvok)
	if (AniMain->AktPrvok!=AniMain->Animacia)
    	AniMain->AktPrvok->SetAlfa(AniMain->AktPrvok->GetAlfa()+R->Value);


AniMain->Zmena=1;
}
//---------------------------------------------------------------------------

void __fastcall TTools::BtnPrevClick(TObject *Sender)
{
/*
if (AniMain->AktScena)
	if (AniMain->AktScena!=((CGrupa*)AniMain->Animacia)->GetPrvy())  {
        AniMain->AktScena=((CScena*)((CPrvok*)AniMain->AktScena)->predosly);
        AniMain->AktScenaNode=AniMain->AktScenaNode->getPrevSibling();
        if (ObjectList)
    	    ObjectList->T->Selected=AniMain->AktScenaNode;
        AniMain->Zmena=1;
    }
    */
AniMain->Previousstate1Click(Sender);
}
//---------------------------------------------------------------------------
void __fastcall TTools::BtnNextClick(TObject *Sender)
{
AniMain->Nextstate1Click(Sender);
}
//---------------------------------------------------------------------------
void __fastcall TTools::BtnFirstClick(TObject *Sender)
{
/*
if (AniMain->AktScena)
	if (AniMain->AktScena!=((CScena*)AniMain->Animacia)->GetPrvy())  {
        AniMain->AktScena=((CScena*)((CGrupa*)AniMain->Animacia)->GetPrvy());
        AniMain->AktScenaNode=AniMain->AnimaciaNode->getFirstChild();
        if (ObjectList)
    	    ObjectList->T->Selected=AniMain->AktScenaNode;
        AniMain->Zmena=1;
    }
    */
AniMain->First1Click(Sender);
}
//---------------------------------------------------------------------------

void __fastcall TTools::BtnLastClick(TObject *Sender)
{
/*
 if (AniMain->AktScena)
	if (AniMain->AktScena!=((CScena*)AniMain->Animacia)->GetPosledny())  {
        AniMain->AktScena=((CScena*)((CGrupa*)AniMain->Animacia)->GetPosledny());
        AniMain->AktScenaNode=AniMain->AnimaciaNode->GetLastChild();
        if (ObjectList)
    	    ObjectList->T->Selected=AniMain->AktScenaNode;
        AniMain->Zmena=1;
    }
    */
    AniMain->Last1Click(Sender);
}
//---------------------------------------------------------------------------

void __fastcall TTools::XClick(TObject *Sender)
{
if (AniMain->AktScena) {
AniMain->AktPrvok=AniMain->AktScena->predna;
AniMain->AktScena->predna->aktualna=true;
AniMain->AktScena->bocna->aktualna=false;
AniMain->AktScena->horna->aktualna=false;
AniMain->Zmena=1;
}
}
//---------------------------------------------------------------------------

void __fastcall TTools::YClick(TObject *Sender)
{
if (AniMain->AktScena) {
AniMain->AktPrvok=AniMain->AktScena->bocna;
AniMain->AktScena->predna->aktualna=false;
AniMain->AktScena->bocna->aktualna=true;
AniMain->AktScena->horna->aktualna=false;
AniMain->Zmena=1;
}
}
//---------------------------------------------------------------------------

void __fastcall TTools::ZClick(TObject *Sender)
{
if (AniMain->AktScena) {
AniMain->AktPrvok=AniMain->AktScena->horna;
AniMain->AktScena->predna->aktualna=false;
AniMain->AktScena->bocna->aktualna=false;
AniMain->AktScena->horna->aktualna=true;
AniMain->Zmena=1;
}
}
//---------------------------------------------------------------------------

void __fastcall TTools::XYZClick(TObject *Sender)
{
if (AniMain->AktScena) {
AniMain->AktPrvok=AniMain->AktScena->GetPrvy();
AniMain->AktPrvokNode=AniMain->AktScenaNode->getFirstChild();
AniMain->AktScena->predna->aktualna=false;
AniMain->AktScena->bocna->aktualna=false;
AniMain->AktScena->horna->aktualna=false;
AniMain->Zmena=1;
}
}
//---------------------------------------------------------------------------


void __fastcall TTools::Timer1Timer(TObject *Sender)
{
return;
}
//---------------------------------------------------------------------------

