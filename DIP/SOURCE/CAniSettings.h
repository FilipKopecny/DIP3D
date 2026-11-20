//---------------------------------------------------------------------------
#ifndef CAniSettingsH
#define CAniSettingsH
//---------------------------------------------------------------------------
#include <vcl\Classes.hpp>
#include <vcl\Controls.hpp>
#include <vcl\StdCtrls.hpp>
#include <vcl\Forms.hpp>
#include <vcl\ComCtrls.hpp>
//---------------------------------------------------------------------------
class TAniSettings : public TForm
{
__published:	// IDE-managed Components
	TLabel *Label1;
	TEdit *FPS;
	TUpDown *UDFps;
	TButton *BtnOk;
	TButton *BtnCancel;
	void __fastcall UDFpsClick(TObject *Sender, TUDBtnType Button);
	void __fastcall FormShow(TObject *Sender);
	void __fastcall FPSChange(TObject *Sender);
    void __fastcall BtnOkClick(TObject *Sender);
private:	// User declarations
public:		// User declarations
	virtual __fastcall TAniSettings(TComponent* Owner);
};
//---------------------------------------------------------------------------
extern TAniSettings *AniSettings;
//---------------------------------------------------------------------------
#endif
