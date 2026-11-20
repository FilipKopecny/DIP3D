//---------------------------------------------------------------------------

#include <vcl\vcl.h>

#pragma hdrstop
#include <windows.h>
//#include "stdafx.h"

#include "CAniMain.h"
#include "CAbout.h"
#include "CGenSettings.h"

#include "CTools.h"
#include "CObjectList.h"
#include "CAniSettings.h"
#include "proj.h"

#define SWAPBUFFERS SwapBuffers(ghDC)

    HDC   ghDC;
    HGLRC ghRC;
    PAINTSTRUCT    ps;
//---------------------------------------------------------------------------
#pragma resource "*.dfm"
TAniMain *AniMain;
//---------------------------------------------------------------------------

__fastcall TAniMain::TAniMain(TComponent* Owner)
	: TForm(Owner)
{


}
//----------------------------------------------------------------------------
GLvoid initializeGL(GLsizei width, GLsizei height)
{
    GLfloat    aspect;

    glMatrixMode( GL_PROJECTION );
      aspect = (GLfloat) width / height;
    glLoadIdentity();
	gluPerspective(35.0, aspect, 0.01, 100.0 );

    gluLookAt(0.0, 0.0, 10.0,
                             0.0, 0.0, 0.0,
                                            0.0,1.0,0.0);


    glMatrixMode( GL_MODELVIEW );
  //  glDepthRange(0.0,100.0);
    glClearColor (0.0, 0.0, 0.0, 0.0);
    glClearDepth(100.0);
    glClear(GL_COLOR_BUFFER_BIT | GL_DEPTH_BUFFER_BIT);

    glLightModeli(GL_LIGHT_MODEL_LOCAL_VIEWER, GL_TRUE);

   	glEnable(GL_DEPTH_TEST);
    glDepthFunc(GL_LEQUAL);

    AniMain->anti=0;
    AniMain->alfa=0;



    glCullFace(GL_BACK);
    glDisable(GL_CULL_FACE);

    glEnable(GL_AUTO_NORMAL);
    glEnable(GL_NORMALIZE);




    glPixelStorei(GL_UNPACK_ALIGNMENT, 1);
	glTexEnvf(GL_TEXTURE_ENV, GL_TEXTURE_ENV_MODE, GL_MODULATE);
    glTexParameterf(GL_TEXTURE_2D, GL_TEXTURE_WRAP_S, GL_REPEAT);
    glTexParameterf(GL_TEXTURE_2D, GL_TEXTURE_WRAP_T, GL_REPEAT);
    glTexParameterf(GL_TEXTURE_2D, GL_TEXTURE_MAG_FILTER, GL_LINEAR);
    glTexParameterf(GL_TEXTURE_2D, GL_TEXTURE_MIN_FILTER, GL_LINEAR);




	glEnable(GL_TEXTURE_2D);


	glEnable(GL_LIGHTING);

	glEnable(GL_MAP2_VERTEX_3);
	glEnable(GL_MAP2_TEXTURE_COORD_2);


	// tuto raz vytvor ..., aby sa stale nemusela tvorit a rusit:
	qobj=gluNewQuadric();
	gluQuadricTexture(qobj, TRUE);

	// na zaciatku vsetko nakresli:
	PostMessage(AniMain->Handle, WM_PAINT, 0, 0L);
}
//---------------------------------------------------------------------------
GLvoid resize(GLsizei width, GLsizei height )
{
    GLfloat aspect;

    glViewport( 0, 0, width, height );

    aspect = (GLfloat) width / height;

    glMatrixMode( GL_PROJECTION );
    glLoadIdentity();
    gluPerspective( 35.0, aspect, 0.01, 100.0 );
    glMatrixMode( GL_MODELVIEW );
    AniMain->Zmena=1;
}

//---------------------------------------------------------------------------
BOOL bSetupPixelFormat(HDC hdc)
{
    PIXELFORMATDESCRIPTOR pfd, *ppfd;
    int pixelformat;

    ppfd = &pfd;

    ppfd->nSize = sizeof(PIXELFORMATDESCRIPTOR);
    ppfd->nVersion = 1;
    ppfd->dwFlags =  PFD_DRAW_TO_WINDOW | PFD_SUPPORT_OPENGL | PFD_DOUBLEBUFFER | PFD_GENERIC_ACCELERATED	;
    ppfd->dwLayerMask = PFD_MAIN_PLANE;
    ppfd->iPixelType = PFD_TYPE_RGBA;
    ppfd->cColorBits = 24;
    ppfd->cDepthBits = 16;
    ppfd->cAlphaBits = 0;
    ppfd->cAccumBits = 0;
    ppfd->cStencilBits = 0;

    pixelformat = ChoosePixelFormat(hdc, ppfd);

    if ( (pixelformat = ChoosePixelFormat(hdc, ppfd)) == 0 )
    {
        MessageBox(NULL, "ChoosePixelFormat failed", "Error", MB_OK);
        return FALSE;
    }




    if (SetPixelFormat(hdc, pixelformat, ppfd) == FALSE)
    {
        MessageBox(NULL, "SetPixelFormat failed", "Error", MB_OK);
        return FALSE;
    }

    return TRUE;

}


//-------------------------------------------------------------------------------
void TAniMain::PridajSub(TTreeNode *nod,CPrvok *temp)
{
while (temp) {
   	if (temp->GetTypPrvku()==TYP_SCENA || temp->GetTypPrvku()==TYP_GRUPA) {
        PridajSub (ObjectList->T->Items->AddChildObject(nod,((CPrvok*)temp)->GetName(),temp),((CGrupa*)temp)->GetPrvy());
       	}
    else {
		ObjectList->T->Items->AddChildObject(nod,((CPrvok*)temp)->GetName(),temp);
    }
    temp = temp->dalsi;
}

}
// ----------------------------------------------------------------------------
void TAniMain::SetAktPrvokNodeIfPossible(void)
{
AniMain->AktPrvokNode=NULL;
    if (ObjectList->T->Selected) {
		AniMain->AktPrvokNode=ObjectList->T->Selected;
       	AniMain->AktPrvok=((CPrvok*)ObjectList->T->Selected->Data);
    }
}
//---------------------------------------------------------------------------
void __fastcall TAniMain::Quit1Click(TObject *Sender)
{
//Form1->delete(Form1);
Application->Terminate();
}
//---------------------------------------------------------------------------
void __fastcall TAniMain::Settings1Click(TObject *Sender)
{
GenSettings->ShowModal();
}
//---------------------------------------------------------------------------
void __fastcall TAniMain::Open1Click(TObject *Sender)
{

 /*
nods->Free(); nods = new TTreeNodes(ObjectList->T);
nod->Free(); nod = new TTreeNode(nods);
   */


ifstream vstup;
if (OpenDialog->Execute()) {
	vstup.open(OpenDialog->FileName.c_str());
	if(vstup.good()) {
        if (AktPrvok)  AktPrvok = NULL;
        if (AktPrvokParent)  AktPrvokParent = NULL;
        if (temp)  temp = NULL;
        if (AktPrvokNode)  AktPrvokNode = NULL;
        if (AktPrvokParentNode)  AktPrvokParentNode = NULL;
        if (AktScenaNode)  AktScenaNode = NULL;
        if (AktScena)  AktScena = NULL;
        if (AnimaciaNode)  AnimaciaNode = NULL;

        char tmp[20];
		vstup >> tmp;
        if (VratTyp(tmp)==TYP_ANIMACIA) {
	 		Animacia=new CAnimacia("pokus");
            if(!Animacia->LoadYou(vstup)){
            	MessageBox (NULL,"Error Reading Ani File","Fatal",MB_OK);
                delete Animacia;
                Animacia = NULL;
                return;
            }

            if (ObjectList) {
               	ObjectList->T->Items->Clear();
                nod=nods->GetFirstNode();
				ObjectList->T->Items->BeginUpdate();
				nod=ObjectList->T->Items->AddObject(nod,((CPrvok*)Animacia)->GetName(),Animacia);
				AnimaciaNode=nod;

				temp = Animacia->GetPrvy();
                if(temp) {
                PridajSub(nod,temp);
                }

				ObjectList->T->Items->EndUpdate();
    		}
		}
	}
}
Zmena=1;
}
//---------------------------------------------------------------------------
void __fastcall TAniMain::Save1Click(TObject *Sender)
{
ofstream vystup;
if(SaveDialog->Execute())  {
    vystup.open(SaveDialog->FileName.c_str());
	if (vystup.good())
    	Animacia->SaveYou(vystup);
    }


}
//---------------------------------------------------------------------------
void __fastcall TAniMain::Tools1Click(TObject *Sender)
{
if (Tools1->Checked)
    {
	Tools1->Checked = false;
	Tools->Hide();
	}
else
	{
	Tools->Show();
	Tools1->Checked = true;
    }
}
//---------------------------------------------------------------------------

void __fastcall TAniMain::ObjectList1Click(TObject *Sender)
{
if (ObjectList1->Checked)
    {
	ObjectList1->Checked = false;
	ObjectList->Hide();
	}
else
	{
	ObjectList->Show();
	ObjectList1->Checked = true;
    }

}
//---------------------------------------------------------------------------
void __fastcall TAniMain::FormShow(TObject *Sender)
{
AniMain->ClientWidth=DEFAULTWIDTH;
AniMain->ClientHeight=DEFAULTHEIGHT;

Tools1->Checked = true;

ObjectList1->Checked = true;
fillstyle=GLU_LINE;

nods =NULL;
nod=NULL;
AktScena=NULL;
Animacia=NULL;
AktPrvok=NULL;
AktPrvokParent=NULL;
temp=NULL;
AktPrvokNode=NULL;
AktPrvokParentNode=NULL;
AktScenaNode=NULL;
AnimaciaNode=NULL;


}
//---------------------------------------------------------------------------
void __fastcall TAniMain::FormClose(TObject *Sender, TCloseAction &Action)
{
 if (ghRC)
            wglDeleteContext(ghRC);
        if (ghDC)
            ReleaseDC(AniMain->Handle, ghDC);
        ghRC = 0;
        ghDC = 0;


Application->Terminate();
}
//---------------------------------------------------------------------------

//---------------------------------------------------------------------------
void __fastcall TAniMain::About2Click(TObject *Sender)
{
About->ShowModal();
}
//---------------------------------------------------------------------------
void __fastcall TAniMain::Settings3Click(TObject *Sender)
{
AniSettings->ShowModal();
}
//---------------------------------------------------------------------------
void __fastcall TAniMain::Light1Click(TObject *Sender)
{
Novy=1;
DSvetlo->ShowModal();
}
//---------------------------------------------------------------------------
void __fastcall TAniMain::Camera1Click(TObject *Sender)
{
Novy=1;
DKamera->ShowModal();
}
//---------------------------------------------------------------------------
void __fastcall TAniMain::Triangle1Click(TObject *Sender)
{
Novy=1;
DTrojuholnik->ShowModal();
}
//---------------------------------------------------------------------------
void __fastcall TAniMain::Rectangle1Click(TObject *Sender)
{
Novy=1;
DObdlznik->ShowModal();
}
//---------------------------------------------------------------------------
void __fastcall TAniMain::Sphere1Click(TObject *Sender)
{
Novy=1;
DDisk->ShowModal();
}
//---------------------------------------------------------------------------
void __fastcall TAniMain::Cube1Click(TObject *Sender)
{
Novy=1;
DKvader->ShowModal();
}
//---------------------------------------------------------------------------
void __fastcall TAniMain::Cone1Click(TObject *Sender)
{
Novy=1;
DKuzel->ShowModal();
}
//---------------------------------------------------------------------------
void __fastcall TAniMain::Disc1Click(TObject *Sender)
{
Novy=1;
DGula->ShowModal();
}
//---------------------------------------------------------------------------
void __fastcall TAniMain::Toroid1Click(TObject *Sender)
{
Novy=1;
DToroid->ShowModal();
}
//---------------------------------------------------------------------------
void __fastcall TAniMain::New1Click(TObject *Sender)
{
if (Animacia) delete Animacia;
Animacia = NULL;


if (ObjectList) {
	ObjectList->T->Items->Clear();
    }

//if (nods)  nods = NULL;
//if (nod)  nod = NULL;
if (AktPrvok)  AktPrvok = NULL;
if (AktPrvokParent)  AktPrvokParent = NULL;
if (temp)  temp = NULL;
if (AktPrvokNode)  AktPrvokNode = NULL;
if (AktPrvokParentNode)  AktPrvokParentNode = NULL;
if (AktScenaNode)  AktScenaNode = NULL;
if (AktScena)  AktScena = NULL;
if (AnimaciaNode)  AnimaciaNode = NULL;


nod=nods->GetFirstNode();
Animacia = new CAnimacia("Animácia");
AktScena = new CScena("Scena");
((CGrupa*)Animacia)->PridajPrvok(AktScena);
/*
nods->Free(); nods = new TTreeNodes(ObjectList->T);
nod->Free(); nod = new TTreeNode(nods);
  */
if (ObjectList) {
	ObjectList->T->Items->BeginUpdate();
	nod=ObjectList->T->Items->AddObject(nod,((CPrvok*)Animacia)->GetName(),Animacia);
	AnimaciaNode=nod;
	nod=ObjectList->T->Items->AddChildObject(nod,((CPrvok*)AktScena)->GetName(),AktScena);
    AktScenaNode=nod;
	ObjectList->T->Items->EndUpdate();
}
Zmena=1;
}
//---------------------------------------------------------------------------
void __fastcall TAniMain::Timer1Timer(TObject *Sender)
{
/*
if (AktScena)
	AS->Text = ((CPrvok*)AktScena)->GetName();
else
	AS->Text = "Null";


if (Animacia)
	A->Text = ((CPrvok*)Animacia)->GetName();
else
	A->Text = "Null";


if (AktPrvok) {

	AP->Text = ((CPrvok*)AktPrvok)->GetName();
	if (((CPrvok*)AktPrvok)->GetTypPrvku()==TYP_GRUPA ||
    ((CPrvok*)AktPrvok)->GetTypPrvku()==TYP_SCENA ||
    ((CPrvok*)AktPrvok)->GetTypPrvku()==TYP_ANIMACIA)
    	if (((CGrupa*)AktPrvok)->GetPrvy())
    		GP->Text = ((CGrupa*)AktPrvok)->GetPrvy()->GetName();
        else
			GP->Text = "Null";
   	else
		GP->Text = "Asi nie";
    }
else
	AP->Text = "Null";


if (AktPrvokParent)
	APP->Text = ((CPrvok*)AktPrvokParent)->GetName();
else
	APP->Text = "Null";

if (AktPrvokNode)
	APN->Text = AktPrvokNode->Text;
else
	APN->Text = "Null";

if (AktPrvokParentNode)
	APPN->Text = AktPrvokParentNode->Text;
else
	APPN->Text = "Null";

if (AktScenaNode)
	ASN->Text = AktScenaNode->Text;
else
	ASN->Text = "Null";


if (temp)
	t->Text = ((CPrvok*)temp)->GetName();
else
	t->Text = "Null";
*/

if (AktScena && Zmena)
    {

    Zmena=0;
    AktScena->Render(fillstyle);
    SWAPBUFFERS;

    }

}
//---------------------------------------------------------------------------
void __fastcall TAniMain::Delete1Click(TObject *Sender)
{
ObjectList->Delete1Click(Sender);
}
//---------------------------------------------------------------------------

void __fastcall TAniMain::FormResize(TObject *Sender)
{


resize(ClientWidth, ClientHeight);

}
//---------------------------------------------------------------------------

void __fastcall TAniMain::FormPaint(TObject *Sender)
{

Zmena=1;
/*
BeginPaint(AniMain->Handle, &ps);
PaintWindow(AniMain->Handle);
EndPaint(AniMain->Handle, &ps);
*/


}
//---------------------------------------------------------------------------

void __fastcall TAniMain::FormCreate(TObject *Sender)
{
     ghDC = GetDC(AniMain->Handle);
     if (!bSetupPixelFormat(ghDC))
        PostQuitMessage (0);
     ghRC = wglCreateContext(ghDC);
     wglMakeCurrent(ghDC, ghRC);
     initializeGL(ClientWidth, ClientHeight);


}
//---------------------------------------------------------------------------



void __fastcall TAniMain::Wireframe1Click(TObject *Sender)
{

if (fillstyle==GLU_LINE)
  {
   Wireframe1->Checked=false;
   fillstyle=GLU_FILL;
   }
else
{
    fillstyle=GLU_LINE;
    Wireframe1->Checked=true;
 }
 Zmena=1;
}                
//---------------------------------------------------------------------------


void __fastcall TAniMain::Compute1Click(TObject *Sender)
{

if (SaveDialog2->Execute()) {
    //FormStyle = fsStayOnTop;   // aby v bitmapach neboli ine prekryte okna
    Animacia->Render(fillstyle,ghDC,SaveDialog2->FileName.c_str());
    //FormStyle = fsNormal;
    }
    else
    Animacia->Render(fillstyle,ghDC);
}
//---------------------------------------------------------------------------



void __fastcall TAniMain::State1Click(TObject *Sender)
{

if (AniMain->AktScena)
	if (AniMain->AktScena==((CGrupa*)AniMain->Animacia)->GetPosledny())  {

        /*
		AniMain->temp = new CScena ("CelkomNovaScena"); //toto treba zmenit na nakopirovanie
        ((CScena*)AniMain->temp)->LoadYou(((CPrvok*)AniMain->AktScena));
		AniMain->AktPrvok = AniMain->temp;
		AniMain->AktScena = ((CScena*)AniMain->temp);
        ((CGrupa*)AniMain->Animacia)->PridajPrvok(AniMain->AktScena);*/

        AniMain->temp=AniMain->Animacia->KopirujScenuZa(AniMain->AktScena,AniMain->AktScena);
        if (!AniMain->temp) return;

        if (ObjectList) {
			ObjectList->T->Items->BeginUpdate();
    		AniMain->PridajSub(AniMain->AnimaciaNode,AniMain->temp);
			ObjectList->T->Items->EndUpdate();
		}
	}
    else
    AniMain->AktScena=((CScena*)((CPrvok*)AniMain->AktScena)->dalsi);
    AniMain->AktScenaNode=AniMain->AktScenaNode->getNextSibling();
    if (ObjectList)
    	ObjectList->T->Selected=AniMain->AktScenaNode;
AniMain->Zmena=1;
}
//---------------------------------------------------------------------------

void __fastcall TAniMain::Front1Click(TObject *Sender)
{
if (AniMain->AktScena) {
AniMain->AktPrvok=AniMain->AktScena->predna;
AniMain->AktScena->predna->aktualna=true;
AniMain->AktScena->bocna->aktualna=false;
AniMain->AktScena->horna->aktualna=false;
AniMain->Zmena=1;
Front1->Checked = true;
Top1->Checked = false;
Side1->Checked = false;
Viewcamera1->Checked = false;
}
}
//---------------------------------------------------------------------------

void __fastcall TAniMain::Top1Click(TObject *Sender)
{
if (AniMain->AktScena) {
AniMain->AktPrvok=AniMain->AktScena->horna;
AniMain->AktScena->predna->aktualna=false;
AniMain->AktScena->bocna->aktualna=false;
AniMain->AktScena->horna->aktualna=true;
AniMain->Zmena=1;
Front1->Checked = false;
Top1->Checked = true;
Side1->Checked = false;
Viewcamera1->Checked = false;

}
}
//---------------------------------------------------------------------------

void __fastcall TAniMain::Side1Click(TObject *Sender)
{
if (AniMain->AktScena) {
AniMain->AktPrvok=AniMain->AktScena->bocna;
AniMain->AktScena->predna->aktualna=false;
AniMain->AktScena->bocna->aktualna=true;
AniMain->AktScena->horna->aktualna=false;
AniMain->Zmena=1;
Front1->Checked = false;
Top1->Checked = false;
Side1->Checked = true;
Viewcamera1->Checked = false;

}
}
//---------------------------------------------------------------------------

void __fastcall TAniMain::Viewcamera1Click(TObject *Sender)
{
if (AniMain->AktScena) {
AniMain->AktPrvok=AniMain->AktScena->GetPrvy();
AniMain->AktPrvokNode=AniMain->AktScenaNode->getFirstChild();
AniMain->AktScena->predna->aktualna=false;
AniMain->AktScena->bocna->aktualna=false;
AniMain->AktScena->horna->aktualna=false;
AniMain->Zmena=1;
Front1->Checked = false;
Top1->Checked = false;
Side1->Checked = false;
Viewcamera1->Checked = true;
}
}
//---------------------------------------------------------------------------



void __fastcall TAniMain::View1Click(TObject *Sender)
{
if (AniMain->AktScena) {
Front1->Checked = AniMain->AktScena->predna->aktualna;
Side1->Checked = AniMain->AktScena->bocna->aktualna;
Top1->Checked = AniMain->AktScena->horna->aktualna;

Viewcamera1->Checked = !(Front1->Checked || Side1->Checked || Top1->Checked);
}
}
//---------------------------------------------------------------------------

void __fastcall TAniMain::Last1Click(TObject *Sender)
{
if (AniMain->AktScena)
	if (AniMain->AktScena!=((CScena*)AniMain->Animacia)->GetPosledny())  {
        AniMain->AktScena=((CScena*)((CGrupa*)AniMain->Animacia)->GetPosledny());
        AniMain->AktScenaNode=AniMain->AnimaciaNode->GetLastChild();
        if (ObjectList)
    	    ObjectList->T->Selected=AniMain->AktScenaNode;
        AniMain->Zmena=1;
    }
}
//---------------------------------------------------------------------------

void __fastcall TAniMain::First1Click(TObject *Sender)
{
if (AniMain->AktScena)
	if (AniMain->AktScena!=((CScena*)AniMain->Animacia)->GetPrvy())  {
        AniMain->AktScena=((CScena*)((CGrupa*)AniMain->Animacia)->GetPrvy());
        AniMain->AktScenaNode=AniMain->AnimaciaNode->getFirstChild();
        if (ObjectList)
    	    ObjectList->T->Selected=AniMain->AktScenaNode;
        AniMain->Zmena=1;
    }
}
//---------------------------------------------------------------------------

void __fastcall TAniMain::Previousstate1Click(TObject *Sender)
{

if (AniMain->AktScena)
	if (AniMain->AktScena!=((CGrupa*)AniMain->Animacia)->GetPrvy())  {
        AniMain->AktScena=((CScena*)((CPrvok*)AniMain->AktScena)->predosly);
        AniMain->AktScenaNode=AniMain->AktScenaNode->getPrevSibling();
        if (ObjectList)
    	    ObjectList->T->Selected=AniMain->AktScenaNode;
        AniMain->Zmena=1;
    }
}
//---------------------------------------------------------------------------

void __fastcall TAniMain::Nextstate1Click(TObject *Sender)
{

if (AniMain->AktScena) {
	if (AniMain->AktScena==((CGrupa*)AniMain->Animacia)->GetPosledny())  {

        /*
		AniMain->temp = new CScena ("CelkomNovaScena"); //toto treba zmenit na nakopirovanie
        ((CScena*)AniMain->temp)->LoadYou(((CPrvok*)AniMain->AktScena));
		AniMain->AktPrvok = AniMain->temp;
		AniMain->AktScena = ((CScena*)AniMain->temp);
        ((CGrupa*)AniMain->Animacia)->PridajPrvok(AniMain->AktScena);*/

        AniMain->temp=AniMain->Animacia->KopirujScenuZa(AniMain->AktScena,AniMain->AktScena);
        if (!AniMain->temp) return;

        if (ObjectList) {
			ObjectList->T->Items->BeginUpdate();
    		AniMain->PridajSub(AniMain->AnimaciaNode,AniMain->temp);
			ObjectList->T->Items->EndUpdate();
		}
	}

    AniMain->AktScena=((CScena*)((CPrvok*)AniMain->AktScena)->dalsi);
    AniMain->AktScenaNode=AniMain->AktScenaNode->getNextSibling();

    if (ObjectList)
    	ObjectList->T->Selected=AniMain->AktScenaNode;
    AniMain->Zmena=1;
    }
}
//---------------------------------------------------------------------------

