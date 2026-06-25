import os
import sys

try:
    from pypdf import PdfReader, PdfWriter
except ImportError:
    print("Error: 'pypdf' package is not installed.")
    print("Please activate your virtual environment or install it using: pip install pypdf")
    sys.exit(1)

def split_pdf(pdf_path, pages_per_split=3):
    # Normalize path (remove quotes if copy-pasted)
    pdf_path = pdf_path.strip('\'"')
    
    if not os.path.exists(pdf_path):
        print(f"[-] Error: File not found at '{pdf_path}'")
        return
        
    try:
        reader = PdfReader(pdf_path)
        total_pages = len(reader.pages)
        print(f"\n[+] Successfully opened: {os.path.basename(pdf_path)}")
        print(f"[+] Total pages: {total_pages}")
        
        file_dir = os.path.dirname(pdf_path)
        base_name = os.path.basename(pdf_path)
        file_name_no_ext, ext = os.path.splitext(base_name)
        
        # Create output directory next to original PDF
        output_dir = os.path.join(file_dir, f"{file_name_no_ext}_splits")
        os.makedirs(output_dir, exist_ok=True)
        print(f"[+] Creating split files inside: {output_dir}\n")
        
        part = 1
        for start_idx in range(0, total_pages, pages_per_split):
            writer = PdfWriter()
            end_idx = min(start_idx + pages_per_split, total_pages)
            
            for page_idx in range(start_idx, end_idx):
                writer.add_page(reader.pages[page_idx])
                
            output_filename = f"{file_name_no_ext}_part_{part}{ext}"
            output_filepath = os.path.join(output_dir, output_filename)
            
            with open(output_filepath, "wb") as output_pdf:
                writer.write(output_pdf)
                
            print(f"  * Saved part {part}: {output_filename} (Pages {start_idx + 1} to {end_idx})")
            part += 1
            
        print(f"\n[✓] Success: Split into {part - 1} PDF file(s) successfully!")
    except Exception as e:
        print(f"\n[-] Error splitting PDF: {str(e)}")

if __name__ == "__main__":
    if len(sys.argv) > 1:
        pdf_path = sys.argv[1]
        pages = int(sys.argv[2]) if len(sys.argv) > 2 else 3
    else:
        print("=" * 50)
        print("          PDF SPLITTER TOOL FOR DEMO PREP")
        print("=" * 50)
        pdf_path = input("Enter path to PDF file (drag and drop here): ").strip()
        pages_input = input("Enter number of pages per split file (default: 3): ").strip()
        pages = int(pages_input) if pages_input else 3
        
    split_pdf(pdf_path, pages)
