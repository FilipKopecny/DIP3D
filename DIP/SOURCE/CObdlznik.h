//---------------------------------------------------------------------------
#ifndef CObdlznikH
#define CObdlznikH
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
class TDObdlznik : public TForm
{
__published:	// IDE-managed Components
	TLabel *Label1;
	TLabel *Label11;
	TEdit *E;
	TPanel *Spec;
	TPanel *Amb;
	TPanel *Dif;
	TPanel *Emi;
	TTrackBar *Shininess;
	TCheckBox *CheckBox1;
	TButton *Button1;
	TButton *BtnOk;
	TButton *BtnCancel;
	TColorDialog *ColorDialog1;
	TOpenDialog *DOpen;
    TButton *NT;
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
    TGroupBox *GroupBox2;
    TLabel *Label18;
    TLabel *Label19;
    TCSpinEdit *FY;
    TCSpinEdit *FX;
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
	void __fastcall BtnOkClick(TObject *Sender);
	void __fastcall BtnCancelClick(TObject *Sender);
	void __fastcall FormShow(TObject *Sender);
    void __fastcall NTClick(TObject *Sender);
    void __fastcall GMClick(TObject *Sender);
private:	// User declarations
public:		// User declarations
	virtual __fastcall TDObdlznik(TComponent* Owner);
};
//---------------------------------------------------------------------------
extern TDObdlznik *DObdlznik;
//---------------------------------------------------------------------------
#endif
