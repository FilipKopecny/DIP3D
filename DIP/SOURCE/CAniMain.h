//---------------------------------------------------------------------------
#ifndef pokusH
#define pokusH
#define DEFAULTWIDTH 640
#define DEFAULTHEIGHT 480
//---------------------------------------------------------------------------
#include <vcl\Classes.hpp>
#include <vcl\Controls.hpp>
#include <vcl\StdCtrls.hpp>
#include <vcl\Forms.hpp>
#include <vcl\Menus.hpp>
#include <vcl\ExtCtrls.hpp>
#include <vcl\Buttons.hpp>
#include <vcl\Dialogs.hpp>
#include <vcl\ComCtrls.hpp>
#include "stdlib.h"
#include "proj.h"
//#include
//---------------------------------------------------------------------------
class TAniMain : public TForm
{
__published:	// IDE-managed Components
	TMainMenu *Menu;
	TMenuItem *File1;
	TMenuItem *New1;
	TMenuItem *Open1;
	TMenuItem *Save1;
	TMenuItem *N1;
	TMenuItem *Settings1;
	TMenuItem *N4;
	TMenuItem *Quit1;
	TMenuItem *Edit1;
	TMenuItem *Cut1;
	TMenuItem *Copy1;
	TMenuItem *Paste1;
	TMenuItem *View1;
	TMenuItem *Wireframe1;
	TMenuItem *Aspect1;
	TMenuItem *Front1;
	TMenuItem *Top1;
	TMenuItem *Side1;
	TMenuItem *Viewcamera1;
	TMenuItem *N3;
	TMenuItem *Tools1;
	TMenuItem *Animation1;
	TMenuItem *Compute1;
	TMenuItem *Help2;
	TMenuItem *About2;
	TMenuItem *Settings3;
	TMenuItem *N5;
	TMenuItem *Delete1;
	TOpenDialog *OpenDialog;
	TSaveDialog *SaveDialog;
	TMenuItem *ObjectList1;
	TMenuItem *N7;
	TMenuItem *Nextstate1;
	TMenuItem *Previousstate1;
	TMenuItem *First1;
	TMenuItem *Last1;
	TPanel *Panel1;
	TEdit *AS;
	TEdit *A;
	TLabel *Label2;
	TLabel *asc;
	TEdit *AP;
	TLabel *Label1;
	TEdit *APPN;
	TLabel *Label3;
	TEdit *APN;
	TLabel *Label4;
	TLabel *Label5;
	TEdit *APP;
	TEdit *ASN;
	TEdit *t;
	TLabel *Label6;
	TLabel *Label7;
	TTimer *Timer1;
	TEdit *GP;
	TLabel *Label8;
	TMenuItem *Object1;
	TMenuItem *Toroid1;
	TMenuItem *Sphere1;
	TMenuItem *Cone1;
	TMenuItem *Cube1;
	TMenuItem *Disc1;
	TMenuItem *Rectangle1;
	TMenuItem *Triangle1;
	TMenuItem *Camera1;
	TMenuItem *Light1;
	TMenuItem *N6;
    TSaveDialog *SaveDialog2;
	void __fastcall Quit1Click(TObject *Sender);
	void __fastcall Settings1Click(TObject *Sender);
	
	void __fastcall Open1Click(TObject *Sender);
	void __fastcall Save1Click(TObject *Sender);
	void __fastcall Tools1Click(TObject *Sender);

	void __fastcall ObjectList1Click(TObject *Sender);

	void __fastcall FormShow(TObject *Sender);
	void __fastcall FormClose(TObject *Sender, TCloseAction &Action);
	
	
	void __fastcall About2Click(TObject *Sender);
	void __fastcall Settings3Click(TObject *Sender);
	
	
	
	void __fastcall Light1Click(TObject *Sender);
	
	void __fastcall Camera1Click(TObject *Sender);
	void __fastcall Triangle1Click(TObject *Sender);
	void __fastcall Rectangle1Click(TObject *Sender);
	void __fastcall Sphere1Click(TObject *Sender);
	void __fastcall Cube1Click(TObject *Sender);
	void __fastcall Cone1Click(TObject *Sender);
	void __fastcall Disc1Click(TObject *Sender);
	void __fastcall Toroid1Click(TObject *Sender);
	void __fastcall New1Click(TObject *Sender);
   

	void __fastcall Timer1Timer(TObject *Sender);
	void __fastcall Delete1Click(TObject *Sender);
    
    void __fastcall FormResize(TObject *Sender);
    void __fastcall FormPaint(TObject *Sender);
    void __fastcall FormCreate(TObject *Sender);
    
    
    void __fastcall Wireframe1Click(TObject *Sender);
    
    void __fastcall Compute1Click(TObject *Sender);
    
    
    void __fastcall State1Click(TObject *Sender);
    void __fastcall Front1Click(TObject *Sender);
    void __fastcall Top1Click(TObject *Sender);
    void __fastcall Side1Click(TObject *Sender);
    void __fastcall Viewcamera1Click(TObject *Sender);
    
    
    void __fastcall View1Click(TObject *Sender);
    void __fastcall Last1Click(TObject *Sender);
    void __fastcall First1Click(TObject *Sender);
    void __fastcall Previousstate1Click(TObject *Sender);
    void __fastcall Nextstate1Click(TObject *Sender);
private:	// User declarations
public:		// User declarations


// smerniky pre komunikaciu okien s vykonnou castou
TTreeNodes *nods ;
TTreeNode *nod;
CScena *AktScena;
CAnimacia *Animacia;
CPrvok *AktPrvok;
CPrvok *AktPrvokParent;
CPrvok *temp;
TTreeNode *AktPrvokNode;
TTreeNode *AktPrvokParentNode;
TTreeNode *AktScenaNode;
TTreeNode *AnimaciaNode;


int Novy;   // ci sa pridava novy prvok
void SetAktPrvokNodeIfPossible(void);  //asi blbost
void PridajSub(TTreeNode* nod,CPrvok* temp); //pomocka pre naplnenie treeview

int Zmena;

enum TypPohladu {Front,Side,Top,Camera};
int fillstyle; //         drot vs. normal
int anti,alfa;

	virtual __fastcall TAniMain(TComponent* Owner);
};
//---------------------------------------------------------------------------
extern TAniMain *AniMain;
//---------------------------------------------------------------------------
#endif
