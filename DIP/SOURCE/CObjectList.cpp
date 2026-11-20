//---------------------------------------------------------------------------
#include <vcl\vcl.h>
#pragma hdrstop

#include "CAniMain.h"
#include "CObjectList.h"
#include "proj.h"
//---------------------------------------------------------------------------
#pragma resource "*.dfm"
TObjectList *ObjectList;

//---------------------------------------------------------------------------
__fastcall TObjectList::TObjectList(TComponent* Owner)
	: TForm(Owner)
{
}
//----------------------------------------------------------------------
void __fastcall TObjectList::NaplnKecy (void)
{
// naplnenie premennych v AniMain
if (T->Selected->Parent) {
	AniMain->AktPrvokParent = ((CPrvok*)T->Selected->Parent->Data);
	AniMain->AktPrvokParentNode = T->Selected->Parent;
    }
else	{
	AniMain->AktPrvokParent = NULL;
	AniMain->AktPrvokParentNode = NULL;
    }

AniMain->AktPrvok=((CPrvok*)T->Selected->Data);
AniMain->AktPrvokNode=T->Selected;
if ( AniMain->AktPrvok->GetTypPrvku()==TYP_SCENA ) {
    AniMain->Zmena=1;
	AniMain->AktScenaNode=T->Selected;
   	AniMain->AktScena=((CScena*)T->Selected->Data);
    }
else {
     if ( AniMain->AktPrvok->GetTypPrvku()!=TYP_ANIMACIA ) {
        AniMain->AktScenaNode = AniMain->AktPrvokNode->Parent; // riskantne, chcelo
        // by to naplnat cez tempNode a az po najdeni to dat do AktScenaNode...
        while (((CPrvok*)AniMain->AktScenaNode->Data)->GetTypPrvku()!=TYP_SCENA){
               AniMain->AktScenaNode = AniMain->AktScenaNode->Parent;
               }
        AniMain->AktScena = ((CScena*)AniMain->AktScenaNode->Data);
        }
     else {
          AniMain->AktScena =((CScena*) AniMain->Animacia->GetPrvy());
          AniMain->AktScenaNode=AniMain->AnimaciaNode->getFirstChild();
          }
     }
}

//---------------------------------------------------------------------------
void __fastcall TObjectList::Light1Click(TObject *Sender)
{
AniMain->Novy=1;
DSvetlo->ShowModal();
}
//---------------------------------------------------------------------------
void __fastcall TObjectList::Camera1Click(TObject *Sender)
{
AniMain->Novy=1;
DKamera->ShowModal();
}
//---------------------------------------------------------------------------
void __fastcall TObjectList::Triangle1Click(TObject *Sender)
{
AniMain->Novy=1;
DTrojuholnik->ShowModal();
}
//---------------------------------------------------------------------------
void __fastcall TObjectList::Rectangle1Click(TObject *Sender)
{
AniMain->Novy=1;
DObdlznik->ShowModal();
}
//---------------------------------------------------------------------------
void __fastcall TObjectList::Sphere1Click(TObject *Sender)
{
AniMain->Novy=1;
DGula->ShowModal();
}
//---------------------------------------------------------------------------
void __fastcall TObjectList::Cube1Click(TObject *Sender)
{
AniMain->Novy=1;
DKvader->ShowModal();
}
//---------------------------------------------------------------------------
void __fastcall TObjectList::Cone1Click(TObject *Sender)
{
AniMain->Novy=1;
DKuzel->ShowModal();
}
//---------------------------------------------------------------------------
void __fastcall TObjectList::Disc1Click(TObject *Sender)
{
AniMain->Novy=1;
DDisk->ShowModal();
}
//---------------------------------------------------------------------------
void __fastcall TObjectList::Toroid1Click(TObject *Sender)
{
AniMain->Novy=1;
DToroid->ShowModal();
}
//---------------------------------------------------------------------------


void __fastcall TObjectList::Edit1Click(TObject *Sender)
{
if (T->Selected) {
	switch (((CPrvok*)T->Selected->Data)->GetTypPrvku()) {

		case TYP_GULA : {  	AniMain->Novy=0;
							DGula->ShowModal();
   	   		 	            break;
   		   		            }
		case TYP_GRUPA : { 	AniMain->Novy=0;
							DGrupa->ShowModal();
       			            break;
							}
        case TYP_DISK :   { AniMain->Novy=0;
							DDisk->ShowModal();
       			            break;
							}
		case TYP_TROJUHOLNIK : {  	AniMain->Novy=0;
							DTrojuholnik->ShowModal();
   	   		 	            break;
   		   		            }
		case TYP_SVETLO : { 	AniMain->Novy=0;
							DSvetlo->ShowModal();
       			            break;
							}
        case TYP_TORUS :   { AniMain->Novy=0;
							DToroid->ShowModal();
       			            break;
							}
		case TYP_OBDLZNIK : { 	AniMain->Novy=0;
							DObdlznik->ShowModal();
       			            break;
							}
        case TYP_MESH :   { AniMain->Novy=0;
							DMesh->ShowModal();
       			            break;
							}
		case TYP_ZREZANY_KUZEL : { 	AniMain->Novy=0;
							DKuzel->ShowModal();
   	   		 	            break;
   		   		            }
		case TYP_KVADER : { 	AniMain->Novy=0;
							DKvader->ShowModal();
       			            break;
							}
		case TYP_SCENA : { 	AniMain->Novy=0;
							DScena->ShowModal();
       			            break;
							}
        case TYP_KAMERA : { AniMain->Novy=0;
                            DKamera->ShowModal();
                            break;
                            }
        default : break;
		}
	}
}
//---------------------------------------------------------------------------
void __fastcall TObjectList::TEdited(TObject *Sender, TTreeNode *Node,
	AnsiString &S)
{
((CPrvok*)Node->Data)->Rename(S.c_str());
}
//---------------------------------------------------------------------------
void __fastcall TObjectList::Group1Click(TObject *Sender)
{
if (AniMain->AktPrvok)
	if (((CPrvok*)AniMain->AktPrvok)->GetTypPrvku()!=TYP_ANIMACIA &&
		((CPrvok*)AniMain->AktPrvok)->GetTypPrvku()!=TYP_SCENA) {
		AniMain->Novy=1;
		DGrupa->ShowModal();
	}
}
//---------------------------------------------------------------------------


void __fastcall TObjectList::Ungroup1Click(TObject *Sender)
{
if (((CPrvok*)AniMain->AktPrvok)->GetTypPrvku()==TYP_GRUPA) {

	while (((CGrupa*)AniMain->AktPrvok)->GetPrvy()) {
    	AniMain->temp=((CGrupa*)AniMain->AktPrvok)->VyhodPrveho();
        T->Items->BeginUpdate();
	 	AniMain->nod=T->Items->AddChildObject(AniMain->AktPrvokParentNode,AniMain->temp->GetName(),AniMain->temp);
        T->Items->EndUpdate();
		((CGrupa*)AniMain->AktPrvokParent)->PridajPrvok(AniMain->temp);
	}

	delete ((CGrupa*)AniMain->AktPrvokParent)->VyradPrvok(AniMain->AktPrvok);
    T->Items->BeginUpdate();
    AniMain->AktPrvokNode->Delete();
    T->Items->EndUpdate();
    AniMain->SetAktPrvokNodeIfPossible();
}
}
//---------------------------------------------------------------------------
void __fastcall TObjectList::TMouseDown(TObject *Sender, TMouseButton Button,
	TShiftState Shift, int X, int Y)
{
if (T->Selected)
	NaplnKecy();
    ShiftPressed =  Shift;
}
//---------------------------------------------------------------------------
void __fastcall TObjectList::TDragDrop(TObject *Sender, TObject *Source, int X,
	int Y)
{
if (AniMain->AktPrvok!=AniMain->Animacia) {
        if (ShiftPressed.Contains(ssShift)) {
        	switch(AniMain->AktPrvok->GetTypPrvku()) {
	        	case TYP_SVETLO			: if (((CPrvok*)T->DropTarget->Data)->GetTypPrvku()==TYP_SCENA) {
                                            AniMain->temp = ((CScena*)T->DropTarget->Data)->VytvorNoveSvetlo( AniMain->AktPrvok );	break;
                                            }
                                            AniMain->temp = AniMain->AktScena->VytvorNoveSvetlo( AniMain->AktPrvok );	break;
		        case TYP_GULA			:   AniMain->temp = new CGula(AniMain->AktPrvok);							break;
        		case TYP_KAMERA			: if (((CPrvok*)T->DropTarget->Data)->GetTypPrvku()==TYP_SCENA) {
                                            AniMain->temp = ((CScena*)T->DropTarget->Data)->VytvorNovuKameru( AniMain->AktPrvok );	break;
                                            }
                                            AniMain->temp = AniMain->AktScena->VytvorNovuKameru( AniMain->AktPrvok );	break;
        		case TYP_DISK			:   AniMain->temp = new CDisk(AniMain->AktPrvok);							break;
        		case TYP_ZREZANY_KUZEL	:	AniMain->temp = new CZrezkuzel(AniMain->AktPrvok);						break;
        		case TYP_MESH			:   AniMain->temp = new CMesh(AniMain->AktPrvok);							break;
        		case TYP_KVADER			:	AniMain->temp = new CKvader(AniMain->AktPrvok);							break;
        		case TYP_OBDLZNIK		:	AniMain->temp = new CObdlznik(AniMain->AktPrvok);						break;
        		case TYP_TROJUHOLNIK	: 	AniMain->temp = new CTrojuholnik(AniMain->AktPrvok);					break;
        		case TYP_TORUS			:	AniMain->temp = new CTorus(AniMain->AktPrvok);							break;
        		case TYP_GRUPA			:   AniMain->temp = new CGrupa("CelkomNovaGrupa");
        		        					((CGrupa*)AniMain->temp)->LoadYou(AniMain->AktPrvok,AniMain->AktScena);
        		default					:   break;
        	}  // koniec switch(aky...
	    	AniMain->AktPrvok = AniMain->temp;

            switch (((CPrvok*)T->DropTarget->Data)->GetTypPrvku()) {

                case TYP_SCENA :
                case TYP_GRUPA : {
    				((CGrupa*)T->DropTarget->Data)->PridajPrvok(AniMain->temp);
                    if (ObjectList) {
		              	ObjectList->T->Items->BeginUpdate();
    		            AniMain->PridajSub(T->DropTarget,AniMain->temp);
			            ObjectList->T->Items->EndUpdate();
		            }
	                break;
    			    } //case typgrupa
                default : break;
	        }  // switch typ target

        }
        else {   // nie copy, ale move
        switch (AniMain->AktPrvok->GetTypPrvku()) {
        case TYP_SCENA : { //presuvame scenu
                           // cize mozeme ju dat len na (za) scenu
                           // a metoda = copy


        					break;
							}
		default : { //hocico ine okrem sceny a animacie

        	    switch (((CPrvok*)T->DropTarget->Data)->GetTypPrvku()) {

                case TYP_SCENA :
                case TYP_GRUPA : {
    				AniMain->temp=((CGrupa*)AniMain->AktPrvokParent)->VyradPrvok(AniMain->AktPrvok);
	    		    ((CGrupa*)T->DropTarget->Data)->PridajPrvok(AniMain->temp);
               		AniMain->AktPrvokNode->MoveTo(T->DropTarget,naAddChild);
                    if (!((CGrupa*)AniMain->AktPrvokParent)->GetPrvy()) {
                    	delete ((CGrupa*)AniMain->AktPrvokParentNode->Parent->Data)->VyradPrvok(AniMain->AktPrvokParent);
				    	AniMain->AktPrvokParentNode->Delete();
                        } // if
                    break;
    			    } //case typgrupa
                default : break;
	        }  // switch typ target
        } // default hocico
    }  // switch typ akt prvok
    } //else
} // if  != Animacia
}
//---------------------------------------------------------------------------
void __fastcall TObjectList::TStartDrag(TObject *Sender,
	TDragObject *&DragObject)
{
//
}
//---------------------------------------------------------------------------
void __fastcall TObjectList::TDragOver(TObject *Sender, TObject *Source, int X,
	int Y, TDragState State, bool &Accept)
{
T->DragCursor = crDrag;
if (T->DropTarget) {
	switch (((CPrvok*)T->Selected->Data)->GetTypPrvku()) {
		case TYP_SCENA : { // scena sa moze klast len na scenu
    		if (((CPrvok*)T->DropTarget->Data)->GetTypPrvku()!=TYP_SCENA)
				T->DragCursor = crNoDrop;
       		break;
        }
		case TYP_ANIMACIA : T->DragCursor = crNoDrop; break;  //animacia sa nekladie nikam
    	default : { // vsetko ostatne sa moze klast na grupu alebo scenu
			if (((CPrvok*)T->DropTarget->Data)->GetTypPrvku()!=TYP_GRUPA &&
            ((CPrvok*)T->DropTarget->Data)->GetTypPrvku()!=TYP_SCENA)
				T->DragCursor = crNoDrop;
            break;
        }
	}
}
}
//---------------------------------------------------------------------------
void __fastcall TObjectList::Delete1Click(TObject *Sender)
{
if(AniMain->AktPrvok)
if (((CPrvok*)AniMain->AktPrvok)->GetTypPrvku()!=TYP_ANIMACIA)
if (((CPrvok*)AniMain->AktPrvok)->GetTypPrvku()!=TYP_SCENA || AniMain->Animacia->GetPrvy() != AniMain->Animacia->GetPosledny())
 	{
    switch (((CPrvok*)AniMain->AktPrvok)->GetTypPrvku()) {
    	case TYP_SCENA : 	delete ((CScena*)((CAnimacia*)AniMain->AktPrvokParent)->VyradPrvok(AniMain->AktPrvok));
        					break;
		case TYP_GRUPA :   	delete ((CGrupa*)((CGrupa*)AniMain->AktPrvokParent)->VyradPrvok(AniMain->AktPrvok));
        					break;
        case TYP_SVETLO :  	AniMain->AktScena->ZrusSvetlo(((CSvetlo*)((CGrupa*)AniMain->AktPrvokParent)->VyradPrvok(AniMain->AktPrvok)));
        					break;
        case TYP_KAMERA :   AniMain->AktScena->ZrusKameru(((CKamera*)((CGrupa*)AniMain->AktPrvokParent)->VyradPrvok(AniMain->AktPrvok)));
                            break;
		default : 		   	delete ((CPrvok*)((CGrupa*)AniMain->AktPrvokParent)->VyradPrvok(AniMain->AktPrvok));
}
    T->Items->BeginUpdate();
    AniMain->AktPrvokNode->Delete();
    T->Items->EndUpdate();
	NaplnKecy(); // naplni kecy v AniMain
    AniMain->Zmena=1;
}

}
//---------------------------------------------------------------------------


void __fastcall TObjectList::FormShow(TObject *Sender)
{
AniMain->nods = new TTreeNodes(ObjectList->T);
AniMain->nod = new TTreeNode(AniMain->nods);

}
//---------------------------------------------------------------------------


