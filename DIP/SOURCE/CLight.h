//---------------------------------------------------------------------------
#ifndef CLightH
#define CLightH
//---------------------------------------------------------------------------
#include <vcl\Classes.hpp>
#include <vcl\Controls.hpp>
#include <vcl\StdCtrls.hpp>
#include <vcl\Forms.hpp>
#include <vcl\ComCtrls.hpp>
#include <vcl\ExtCtrls.hpp>
#include <vcl\Dialogs.hpp>
#include "CSPIN.h"
//---------------------------------------------------------------------------
class TDSvetlo : public TForm
{
__published:	// IDE-managed Components
	TLabel *Label1;
	TEdit *E;
	TPanel *Spec;
	TPanel *Amb;
	TPanel *Dif;
	TPanel *Emi;
    TCheckBox *CEnable;
	TButton *BtnOk;
	TButton *BtnCancel;
	TColorDialog *ColorDialog1;
	TTrackBar *SE;
	TTrackBar *SC;
	TLabel *Label5;
	TLabel *Label6;
	TLabel *Label7;
	TLabel *Label8;
	TGroupBox *LT;
	TRadioButton *RGS;
	TRadioButton *RGO;
	TGroupBox *ATT;
    TTrackBar *TC;
    TTrackBar *TQ;
    TTrackBar *TL;
    TLabel *Label9;
    TLabel *Label10;
    TLabel *Label11;
    TCheckBox *ZP;
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


    TButton *GM;
	void __fastcall SpecClick(TObject *Sender);
	void __fastcall DifClick(TObject *Sender);
	void __fastcall EmiClick(TObject *Sender);
	void __fastcall AmbClick(TObject *Sender);
	void __fastcall BtnOkClick(TObject *Sender);
	void __fastcall BtnCancelClick(TObject *Sender);
	void __fastcall FormShow(TObject *Sender);

    void __fastcall GMClick(TObject *Sender);
private:	// User declarations
public:		// User declarations
	virtual __fastcall TDSvetlo(TComponent* Owner);
};
//---------------------------------------------------------------------------
extern TDSvetlo *DSvetlo;
//---------------------------------------------------------------------------
#endif
