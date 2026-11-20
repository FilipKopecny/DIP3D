//---------------------------------------------------------------------------
#ifndef CGulaH
#define CGulaH
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
class TDGula : public TForm
{
__published:	// IDE-managed Components
	TEdit *E;
	TLabel *Label1;
	TGroupBox *GroupBox1;
	TGroupBox *GroupBox2;
	TLabel *Label5;
	TLabel *Label6;
	TLabel *Label7;
	TGroupBox *GroupBox3;
	TLabel *Label8;
	TLabel *Label9;
	TLabel *Label10;
	TPanel *Spec;
	TPanel *Amb;
	TPanel *Dif;
	TPanel *Emi;
	TTrackBar *Shininess;
	TLabel *Label11;
	TCheckBox *CheckBox1;
	TButton *Button1;
	TButton *BtnOk;
	TButton *BtnCancel;
	TColorDialog *ColorDialog1;
	TOpenDialog *DOpen;
	TLabel *Label2;
	TLabel *Label3;
	TLabel *Label4;
	TLabel *Label12;
	TLabel *Label13;
	TLabel *Label14;
	TLabel *Label15;
	TLabel *Label16;
	TLabel *Label17;
    TButton *NT;
    TCheckBox *ZP;
    TCSpinEdit *PX;
    TCSpinEdit *PY;
    TCSpinEdit *PZ;
    TCSpinEdit *RX;
    TCSpinEdit *RY;
    TCSpinEdit *RZ;
    TCSpinEdit *FX;
    TCSpinEdit *FY;
    TCSpinEdit *FR;
    TGroupBox *GroupBox4;
    TLabel *Label20;
    TLabel *Label21;
    TLabel *Label22;
    TLabel *Label23;
    TCSpinEdit *AS;
    TCSpinEdit *AA;
    TCSpinEdit *AD;
    TCSpinEdit *AE;
    TButton *GM;
	void __fastcall SpecClick(TObject *Sender);
	void __fastcall AmbClick(TObject *Sender);
	void __fastcall DifClick(TObject *Sender);
	void __fastcall EmiClick(TObject *Sender);
	void __fastcall Button1Click(TObject *Sender);
	void __fastcall FormShow(TObject *Sender);
	void __fastcall BtnOkClick(TObject *Sender);
	void __fastcall BtnCancelClick(TObject *Sender);
    void __fastcall NTClick(TObject *Sender);
    void __fastcall GMClick(TObject *Sender);
private:	// User declarations
public:		// User declarations
	virtual __fastcall TDGula(TComponent* Owner);
};
//---------------------------------------------------------------------------
extern TDGula *DGula;
//---------------------------------------------------------------------------
#endif
