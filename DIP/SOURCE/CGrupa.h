//---------------------------------------------------------------------------
#ifndef CGrupaH
#define CGrupaH
//---------------------------------------------------------------------------
#include <vcl\Classes.hpp>
#include <vcl\Controls.hpp>
#include <vcl\StdCtrls.hpp>
#include <vcl\Forms.hpp>
#include <vcl\ComCtrls.hpp>
#include "CSPIN.h"
//---------------------------------------------------------------------------
class TDGrupa : public TForm
{
__published:	// IDE-managed Components
	TGroupBox *GroupBox1;
	TLabel *Label2;
	TLabel *Label3;
	TLabel *Label4;
	TEdit *E;
	TLabel *Label1;
	TGroupBox *GroupBox3;
	TLabel *Label15;
	TLabel *Label16;
	TLabel *Label17;
	TButton *BtnOk;
	TButton *BtnCancel;
	
	
	
	
	
	
	
	
	
	
	
	
    TCheckBox *ZP;
    TCSpinEdit *PX;
    TCSpinEdit *PY;
    TCSpinEdit *PZ;
    TCSpinEdit *RX;
    TCSpinEdit *RY;
    TCSpinEdit *RZ;
    TButton *GM;
    void __fastcall FormShow(TObject *Sender);
	void __fastcall BtnOkClick(TObject *Sender);
	void __fastcall BtnCancelClick(TObject *Sender);
    void __fastcall GMClick(TObject *Sender);
    private:	// User declarations
public:		// User declarations
	virtual __fastcall TDGrupa(TComponent* Owner);
};
//---------------------------------------------------------------------------
extern TDGrupa *DGrupa;
//---------------------------------------------------------------------------
#endif
