//---------------------------------------------------------------------------
#ifndef CToolsH
#define CToolsH
//---------------------------------------------------------------------------
#include <vcl\Classes.hpp>
#include <vcl\Controls.hpp>
#include <vcl\StdCtrls.hpp>
#include <vcl\Forms.hpp>
#include <vcl\Buttons.hpp>
#include <vcl\ExtCtrls.hpp>
#include "CSPIN.h"
//---------------------------------------------------------------------------
class TTools : public TForm
{
__published:	// IDE-managed Components
	TImage *Trojuhol;
	TImage *Obdlznik;
	TImage *Svetlo;
	TImage *Mesh;
	TImage *Disk;
	TImage *Zkuzel;
	TImage *Gula;
	TImage *Toroid;
	TImage *Kvader;
	TImage *Kamera;
	TButton *X;
	TButton *XYZ;
	TImage *XML;
	TImage *XMR;
	TImage *YMH;
	TImage *YMD;
	TImage *ZMP;
	TImage *ZMZ;
	TImage *YRCCW;
	TImage *YRCW;
	TImage *XRCW;
	TImage *XRCCW;
	TImage *ZRCCW;
	TImage *ZRCW;
	TButton *Y;
	TButton *Z;
	TButton *BtnFirst;
	TButton *BtnPrev;
	TButton *BtnNext;
	TButton *BtnLast;
    TCSpinEdit *M;
    TCSpinEdit *R;
    TLabel *Mov;
    TLabel *Rot;
    TTimer *Timer1;
	void __fastcall GulaClick(TObject *Sender);
	void __fastcall TrojuholClick(TObject *Sender);
	void __fastcall ObdlznikClick(TObject *Sender);
	void __fastcall DiskClick(TObject *Sender);
	void __fastcall KvaderClick(TObject *Sender);
	void __fastcall KameraClick(TObject *Sender);
	void __fastcall ZkuzelClick(TObject *Sender);
	void __fastcall ToroidClick(TObject *Sender);
	void __fastcall MeshClick(TObject *Sender);
	void __fastcall SvetloClick(TObject *Sender);

	void __fastcall BtnNextClick(TObject *Sender);
	
	
	
	
	
	
	
	
	
	
	
	
	
	
	
	
	
	
	
	void __fastcall XMRMouseDown(TObject *Sender, TMouseButton Button,
	TShiftState Shift, int X, int Y);
	void __fastcall XMLMouseDown(TObject *Sender, TMouseButton Button,
	TShiftState Shift, int X, int Y);
	void __fastcall ZMZMouseDown(TObject *Sender, TMouseButton Button,
	TShiftState Shift, int X, int Y);
	void __fastcall YRCCWMouseDown(TObject *Sender, TMouseButton Button,
	TShiftState Shift, int X, int Y);
	void __fastcall XRCCWMouseDown(TObject *Sender, TMouseButton Button,
	TShiftState Shift, int X, int Y);
	void __fastcall YMHMouseDown(TObject *Sender, TMouseButton Button,
	TShiftState Shift, int X, int Y);
	void __fastcall YMDMouseDown(TObject *Sender, TMouseButton Button,
	TShiftState Shift, int X, int Y);
	void __fastcall ZMPMouseDown(TObject *Sender, TMouseButton Button,
	TShiftState Shift, int X, int Y);
	void __fastcall YRCWMouseDown(TObject *Sender, TMouseButton Button,
	TShiftState Shift, int X, int Y);
	void __fastcall XRCWMouseDown(TObject *Sender, TMouseButton Button,
	TShiftState Shift, int X, int Y);
	void __fastcall ZRCCWMouseDown(TObject *Sender, TMouseButton Button,
	TShiftState Shift, int X, int Y);
	void __fastcall ZRCWMouseDown(TObject *Sender, TMouseButton Button,
	TShiftState Shift, int X, int Y);
	
	
	
	
	
	
	
    
    void __fastcall BtnPrevClick(TObject *Sender);
    void __fastcall BtnFirstClick(TObject *Sender);
    void __fastcall BtnLastClick(TObject *Sender);
    void __fastcall XClick(TObject *Sender);
    void __fastcall YClick(TObject *Sender);
    void __fastcall ZClick(TObject *Sender);
    void __fastcall XYZClick(TObject *Sender);
    
    void __fastcall Timer1Timer(TObject *Sender);
private:	// User declarations
public:		// User declarations
	virtual __fastcall TTools(TComponent* Owner);
};
//---------------------------------------------------------------------------
extern TTools *Tools;
//---------------------------------------------------------------------------
#endif
