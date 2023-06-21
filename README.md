# PDF Splitter

PDF Splitter is a simple application that splits a PDF document into multiple documents, each containing one page from the original file. The application also allows the user to set the scale, compression, and dpi parameters for the output files.

## Getting Started

To get the application running, follow these steps:

1. Clone the repository:
    ```bash
    git clone https://github.com/yourusername/PdfSplitter.git
    cd PdfSplitter
    ```

2. Install the necessary packages:
    ```bash
    npm install
    ```

3. Start the application:
    ```bash
    npm start
    ```

The application should now be running at `http://localhost:3000`.

## How to Use

1. Open the application in your web browser.

2. Select a PDF file using the "Select File" button.

3. (Optional) Adjust the `scale`, `compression`, and `dpi` parameters in the "Advanced" section.

4. Click "Upload" to process the file. The file will be uploaded, and the PDF will be split into multiple files.

5. Once the file has been processed, a ZIP file containing all the split PDFs will be automatically downloaded.

## Backend Code Explanation

The backend is a simple ASP.NET Core API with one endpoint (`api/upload`) which accepts HTTP POST requests.

- The `Upload` method in the `UploadController` class takes in an `IFormFile` (the uploaded file), and `scale`, `compression`, and `dpi` as parameters. 

- It first validates the parameters, and if they are valid, it reads the uploaded file into a byte array.

- For each page of the PDF, it converts the page to an image with a specified DPI, compresses and converts the image to a PDF, and writes the PDF to a ZIP file.

- When all pages have been processed, it returns the ZIP file.

## Frontend Code Explanation

The frontend is a simple React application:

- The `App` component handles the file upload process and user interaction. 

- The user can choose a file and set `scale`, `compression`, and `dpi` parameters.

- When the user clicks "Upload", the selected file and parameters are sent to the backend using `axios`.

- Once the file is processed, a ZIP file containing the split PDFs will be automatically downloaded.

## Dependencies

- Frontend: The frontend uses React for the UI and axios for HTTP requests.
- Backend: The backend is built with ASP.NET Core and uses iText for PDF processing and ICSharpCode.SharpZipLib for ZIP file handling.

Please note that the backend is deployed at `https://pdf-splitter-kabacinski.azurewebsites.net/api/upload`. If you want to run the backend locally, you will need to adjust the URL in the `axios.post()` method in `App.js`.

## License

This project is licensed under the AGPL-3.0 license - see the [LICENSE.md](LICENSE) file for details.
