//---------------------------------------------------------------------------
#ifndef CScenaH
#define CScenaH
//---------------------------------------------------------------------------
#include <Classes.hpp>
#include <Controls.hpp>
#include <StdCtrls.hpp>
#include <Forms.hpp>
#include <ComCtrls.hpp>
#include <Dialogs.hpp>
#include <ExtCtrls.hpp>
#include "CSPIN.h"
//---------------------------------------------------------------------------
class TDScena : public TForm
{
__published:	// IDE-managed Components
    TPanel *Panel1;
    TLabel *Label11;
    TRadioGroup *RGFogType;
    TRadioButton *RBType2;
    TRadioButton *RBType3;
    TPanel *FogColor;
    TTrackBar *Density;
    TGroupBox *GroupBox2;
    TLabel *Label13;
    TLabel *Label5;
    TColorDialog *ColorDialog1;
    TButton *BtnOk;
    TButton *BtnCancel;
    TPanel *Panel2;
    TRadioButton *RBNone;
    TLabel *Label1;
    TEdit *E;
    TGroupBox *GroupBox1;
    TLabel *Label2;
    TLabel *Label3;
    TLabel *Label4;
    TCSpinEdit *PX;
    TCSpinEdit *PY;
    TCSpinEdit *PZ;
    TGroupBox *GroupBox3;
    TLabel *Label15;
    TLabel *Label16;
    TLabel *Label17;
    TCSpinEdit *RX;
    TCSpinEdit *RY;
    TCSpinEdit *RZ;
    TCSpinEdit *FNear;
    TCSpinEdit *FFar;
    TCSpinEdit *T;
    TLabel *Label6;
    TRadioButton *RBType1;
    void __fastcall FogColorClick(TObject *Sender);
    void __fastcall FormShow(TObject *Sender);
    void __fastcall BtnOkClick(TObject *Sender);
    void __fastcall BtnCancelClick(TObject *Sender);
private:	// User declarations
public:		// User declarations
    __fastcall TDScena(TComponent* Owner);
};
//---------------------------------------------------------------------------
extern PACKAGE TDScena *DScena;
//---------------------------------------------------------------------------
#endif
