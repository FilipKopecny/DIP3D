//---------------------------------------------------------------------------
#ifndef CGenMovtH
#define CGenMovtH
//---------------------------------------------------------------------------
#include <Classes.hpp>
#include <Controls.hpp>
#include <StdCtrls.hpp>
#include <Forms.hpp>
#include "CSPIN.h"
//---------------------------------------------------------------------------
class TDGenMovt : public TForm
{
__published:	// IDE-managed Components
    TCSpinEdit *RT;
    TLabel *Time;
    TLabel *Angle;
    TCSpinEdit *RAX;
    TCSpinEdit *RAZ;
    TCSpinEdit *RAY;
    TCSpinEdit *OCX;
    TCSpinEdit *OCZ;
    TCSpinEdit *OCY;
    TCSpinEdit *OAX;
    TCSpinEdit *OAZ;
    TCSpinEdit *OAY;
    TCSpinEdit *OT;
    TLabel *Label1;
    TLabel *Label2;
    TLabel *Label3;
    TLabel *Label4;
    TLabel *Label5;
    TLabel *X;
    TLabel *Y;
    TLabel *Z;
    TButton *OK;
    TButton *Cancel;
    TCheckBox *RP;
    TCheckBox *OP;
    void __fastcall FormShow(TObject *Sender);
    void __fastcall OKClick(TObject *Sender);
private:	// User declarations
public:		// User declarations
    __fastcall TDGenMovt(TComponent* Owner);
};
//---------------------------------------------------------------------------
extern PACKAGE TDGenMovt *DGenMovt;
//---------------------------------------------------------------------------
#endif
