//---------------------------------------------------------------------------
#include <vcl.h>
#pragma hdrstop

#include "CAniMain.h"
#include "CGenMovt.h"
//---------------------------------------------------------------------------
#pragma package(smart_init)
#pragma link "CSPIN"
#pragma resource "*.dfm"
TDGenMovt *DGenMovt;
//---------------------------------------------------------------------------
__fastcall TDGenMovt::TDGenMovt(TComponent* Owner)
    : TForm(Owner)
{
}
//---------------------------------------------------------------------------
void __fastcall TDGenMovt::FormShow(TObject *Sender)
{

RT->Text = AniMain->temp->GetRPeriod();
OT->Text = AniMain->temp->GetOPeriod();
RAX->Text = AniMain->temp->GetRAngleX();
RAY->Text = AniMain->temp->GetRAngleY();
RAZ->Text = AniMain->temp->GetRAngleZ();

OAX->Text = AniMain->temp->GetOAngleX();
OAY->Text = AniMain->temp->GetOAngleY();
OAZ->Text = AniMain->temp->GetOAngleZ();

OCX->Text = AniMain->temp->GetOCenterX();
OCY->Text = AniMain->temp->GetOCenterY();
OCZ->Text = AniMain->temp->GetOCenterZ();

OP->Checked = AniMain->temp->GetOPermanent();
RP->Checked = AniMain->temp->GetRPermanent();
}
//---------------------------------------------------------------------------

void __fastcall TDGenMovt::OKClick(TObject *Sender)
{

AniMain->temp->SetRPeriod(RT->Text.ToDouble());
AniMain->temp->SetOPeriod(OT->Text.ToDouble());


AniMain->temp->SetRAngleX(RAX->Text.ToDouble());
AniMain->temp->SetRAngleY(RAY->Text.ToDouble());
AniMain->temp->SetRAngleZ(RAZ->Text.ToDouble());

AniMain->temp->SetOAngleX(OAX->Text.ToDouble());
AniMain->temp->SetOAngleY(OAY->Text.ToDouble());
AniMain->temp->SetOAngleZ(OAZ->Text.ToDouble());

AniMain->temp->SetOCenterX(OCX->Text.ToDouble());
AniMain->temp->SetOCenterY(OCY->Text.ToDouble());
AniMain->temp->SetOCenterZ(OCZ->Text.ToDouble());


AniMain->temp->SetOPermanent(OP->Checked);
AniMain->temp->SetRPermanent(RP->Checked);
}
//---------------------------------------------------------------------------

