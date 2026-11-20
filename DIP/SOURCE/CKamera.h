//---------------------------------------------------------------------------
#ifndef CKameraH
#define CKameraH
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
class TDKamera : public TForm
{
__published:	// IDE-managed Components
	TLabel *Label1;
	TLabel *Label8;
	TLabel *Label9;
	TLabel *Label10;
	TGroupBox *GroupBox1;
	TLabel *Label2;
	TLabel *Label3;
	TLabel *Label4;
	TEdit *E;
	TGroupBox *GroupBox3;
	TLabel *Label15;
	TLabel *Label16;
	TLabel *Label17;
	TCheckBox *CActual;
	TButton *BtnOk;
	TButton *BtnCancel;
	TGroupBox *GroupBox2;
	TLabel *FAng;
    TCheckBox *ZP;
    TCSpinEdit *PX;
    TCSpinEdit *PY;
    TCSpinEdit *PZ;
    TCSpinEdit *RX;
    TCSpinEdit *RY;
    TCSpinEdit *RZ;
    TCSpinEdit *FA;
    TButton *GM;
	void __fastcall BtnOkClick(TObject *Sender);
	void __fastcall BtnCancelClick(TObject *Sender);
	void __fastcall FormShow(TObject *Sender);
    void __fastcall GMClick(TObject *Sender);
private:	// User declarations
public:		// User declarations
	virtual __fastcall TDKamera(TComponent* Owner);
};
//---------------------------------------------------------------------------
extern TDKamera *DKamera;
//---------------------------------------------------------------------------
#endif
