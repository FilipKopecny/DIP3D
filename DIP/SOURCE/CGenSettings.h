//---------------------------------------------------------------------------
#ifndef CGenSettingsH
#define CGenSettingsH
//---------------------------------------------------------------------------
#include <vcl\Classes.hpp>
#include <vcl\Controls.hpp>
#include <vcl\StdCtrls.hpp>
#include <vcl\Forms.hpp>
#include <vcl\ComCtrls.hpp>
//---------------------------------------------------------------------------
class TGenSettings : public TForm
{
__published:	// IDE-managed Components
	TGroupBox *GBFrameSize;
	TRadioButton *RB320;
	TRadioButton *RB640;
	TRadioButton *RB800;
	TButton *BtnOk;
	TButton *BtnCancel;
    TCheckBox *AA;
    TCheckBox *AB;
	TRadioButton *RB1024;

	void __fastcall FormShow(TObject *Sender);

	void __fastcall BtnOkClick(TObject *Sender);
	private:	// User declarations
public:		// User declarations
	virtual __fastcall TGenSettings(TComponent* Owner);
};
//---------------------------------------------------------------------------
extern TGenSettings *GenSettings;
//---------------------------------------------------------------------------
#endif
