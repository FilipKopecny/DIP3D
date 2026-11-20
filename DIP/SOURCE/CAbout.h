//---------------------------------------------------------------------------
#ifndef CAboutH
#define CAboutH
//---------------------------------------------------------------------------
#include <vcl\Classes.hpp>
#include <vcl\Controls.hpp>
#include <vcl\StdCtrls.hpp>
#include <vcl\Forms.hpp>
//---------------------------------------------------------------------------
class TAbout : public TForm
{
__published:	// IDE-managed Components
	TMemo *Memo1;
	TButton *BtnOk;
private:	// User declarations
public:		// User declarations
	virtual __fastcall TAbout(TComponent* Owner);
};
//---------------------------------------------------------------------------
extern TAbout *About;
//---------------------------------------------------------------------------
#endif
