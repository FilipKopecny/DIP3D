//---------------------------------------------------------------------------
#ifndef CObjectListH
#define CObjectListH
//---------------------------------------------------------------------------
#include <vcl\Classes.hpp>
#include <vcl\Controls.hpp>
#include <vcl\StdCtrls.hpp>
#include <vcl\Forms.hpp>
#include <vcl\ComCtrls.hpp>
#include <vcl\Menus.hpp>
#include "proj.h"
//---------------------------------------------------------------------------
class TObjectList : public TForm
{
__published:	// IDE-managed Components
	TTreeView *T;
	TPopupMenu *TreeMenu;
	TMenuItem *Ungroup1;
	TMenuItem *Edit1;
	TMenuItem *Delete1;
	TMenuItem *N1;
	TMenuItem *Insert1;
	TMenuItem *Toroid1;
	TMenuItem *Sphere1;
	TMenuItem *Cone1;
	TMenuItem *Cube1;
	TMenuItem *Disc1;
	TMenuItem *Rectangle1;
	TMenuItem *Triangle1;
	TMenuItem *Camera1;
	TMenuItem *Light1;
	TMenuItem *Group1;
	void __fastcall Light1Click(TObject *Sender);
	void __fastcall Camera1Click(TObject *Sender);
	void __fastcall Triangle1Click(TObject *Sender);
	void __fastcall Rectangle1Click(TObject *Sender);
	void __fastcall Disc1Click(TObject *Sender);
	void __fastcall Cube1Click(TObject *Sender);
	void __fastcall Cone1Click(TObject *Sender);
	void __fastcall Sphere1Click(TObject *Sender);
	void __fastcall Toroid1Click(TObject *Sender);
    void __fastcall NaplnKecy (void);
	
	void __fastcall Edit1Click(TObject *Sender);
	void __fastcall TEdited(TObject *Sender, TTreeNode *Node, AnsiString &S);
	void __fastcall Group1Click(TObject *Sender);
	void __fastcall Ungroup1Click(TObject *Sender);
	
	
	void __fastcall TMouseDown(TObject *Sender, TMouseButton Button,
	TShiftState Shift, int X, int Y);
	
	
	
	
	
	
	
	void __fastcall TDragDrop(TObject *Sender, TObject *Source, int X, int Y);
	void __fastcall TStartDrag(TObject *Sender, TDragObject *&DragObject);
	void __fastcall TDragOver(TObject *Sender, TObject *Source, int X, int Y,
	TDragState State, bool &Accept);
	void __fastcall Delete1Click(TObject *Sender);
	void __fastcall FormShow(TObject *Sender);
    
    
private:	// User declarations
public:		// User declarations
    TShiftState ShiftPressed;
	virtual __fastcall TObjectList(TComponent* Owner);
};
//---------------------------------------------------------------------------
extern TObjectList *ObjectList;
//---------------------------------------------------------------------------
#endif
