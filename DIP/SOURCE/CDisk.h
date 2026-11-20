//---------------------------------------------------------------------------
#ifndef CDiskH
#define CDiskH
//---------------------------------------------------------------------------
#include <vcl\Classes.hpp>
#include <vcl\Controls.hpp>
#include <vcl\StdCtrls.hpp>
#include <vcl\Forms.hpp>
#include <vcl\ComCtrls.hpp>
#include <vcl\Dialogs.hpp>

#include <ExtCtrls.hpp>
#include "CSPIN.h"

//---------------------------------------------------------------------------
class TDDisk : public TForm
{
__published:	// IDE-managed Components
	TLabel *Label1;
	TLabel *Label5;
	TLabel *Label6;
	TLabel *Label7;
	TLabel *Label8;
	TLabel *Label9;
	TLabel *Label10;
	TLabel *Label11;
	TEdit *E;
	TGroupBox *GroupBox2;
	TLabel *Label12;
	TLabel *Label13;
	TLabel *Label14;
	TGroupBox *GroupBox3;
	TLabel *Label15;
	TLabel *Label16;
	TLabel *Label17;
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
	TLabel *Label18;
	TLabel *Label19;
    TGroupBox *GroupBox4;
    TButton *NT;
    TLabel *Label20;
    TLabel *Label21;
    TLabel *Label22;
    TLabel *Label23;
    TCheckBox *ZP;
    TCSpinEdit *AS;
    TCSpinEdit *AA;
    TCSpinEdit *AD;
    TCSpinEdit *AE;
    TCSpinEdit *RX;
    TCSpinEdit *RY;
    TCSpinEdit *RZ;
    TCSpinEdit *FRin;
    TCSpinEdit *FRout;
    TCSpinEdit *FAngle;
    TCSpinEdit *FY;
    TCSpinEdit *FX;
    TGroupBox *GroupBox1;
    TLabel *Label2;
    TLabel *Label3;
    TLabel *Label4;
    TCSpinEdit *PX;
    TCSpinEdit *PY;
    TCSpinEdit *PZ;
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
	virtual __fastcall TDDisk(TComponent* Owner);
};
//---------------------------------------------------------------------------
extern TDDisk *DDisk;
//---------------------------------------------------------------------------
#endif
